using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using FluentValidation;
using JobFinder.Api.Config;
using JobFinder.Api.Data;
using JobFinder.Api.Middleware;
using JobFinder.Api.Queue;
using JobFinder.Api.Services;
using JobFinder.Api.Utils;
using JobFinder.Api.Validators;
using JobFinder.Api.Common.Models;
// Alias to avoid ambiguity with StackExchange.Redis.Role
using AppModels = JobFinder.Api.Common.Models;

namespace JobFinder.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // 1. Load local .env file if it exists (for local development outside Docker)
            LoadEnvFile(Path.Combine(AppContext.BaseDirectory, "../../../../.env"));
            LoadEnvFile(Path.Combine(AppContext.BaseDirectory, ".env"));

            var builder = WebApplication.CreateBuilder(args);

            // 2. Parse and Validate Configurations
            var envConfig = new EnvConfig
            {
                Port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port) ? port : 5002,
                DatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ?? builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
                RedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? builder.Configuration.GetSection("Redis")["ConnectionString"] ?? string.Empty,
                AccessTokenSecret = Environment.GetEnvironmentVariable("ACCESS_TOKEN_SECRET") ?? builder.Configuration.GetSection("Jwt")["AccessTokenSecret"] ?? string.Empty,
                RefreshTokenSecret = Environment.GetEnvironmentVariable("REFRESH_TOKEN_SECRET") ?? builder.Configuration.GetSection("Jwt")["RefreshTokenSecret"] ?? string.Empty,
                AccessTokenExpirationMinutes = int.TryParse(Environment.GetEnvironmentVariable("ACCESS_TOKEN_EXPIRES_IN_MINUTES"), out var accMin) ? accMin : 15,
                RefreshTokenExpirationDays = int.TryParse(Environment.GetEnvironmentVariable("REFRESH_TOKEN_EXPIRES_IN_DAYS"), out var refDays) ? refDays : 7,
                CorsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGIN") ?? builder.Configuration.GetSection("Cors")["AllowedOrigins"] ?? "http://localhost:3000",
                QueueConcurrency = int.TryParse(Environment.GetEnvironmentVariable("QUEUE_CONCURRENCY"), out var concurrency) ? concurrency : 5
            };
            envConfig.Validate();
            builder.Services.AddSingleton(envConfig);

            // Configure Kestrel Server Port
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(envConfig.Port);
            });

            // 3. Register EF Core DbContext with Npgsql
            // Registering enums inside UseNpgsql options ensures the EF Core migrations
            // tool detects the mapped enums at design-time and generates the correct
            // PostgreSQL enum column types instead of falling back to integers.
            var prismaTranslator = NpgsqlPrismaNameTranslator.Instance;
            builder.Services.AddDbContext<JobFinderDbContext>(options =>
                options.UseNpgsql(envConfig.DatabaseUrl, npgsqlOptions =>
                {
                    npgsqlOptions.MapEnum<AppModels.Role>(nameTranslator: prismaTranslator);
                    npgsqlOptions.MapEnum<AppModels.ApplicationStatus>(nameTranslator: prismaTranslator);
                    npgsqlOptions.MapEnum<AppModels.NotificationType>(nameTranslator: prismaTranslator);
                    npgsqlOptions.MapEnum<AppModels.ReportStatus>(nameTranslator: prismaTranslator);
                }));

            // 4. Register StackExchange.Redis
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(envConfig.RedisUrl));
            builder.Services.AddSingleton<IRedisQueue, RedisQueue>();

            // 5. Register Background Hosted Queue Worker
            builder.Services.AddHostedService<QueueWorker>();

            // 6. Register Services
            builder.Services.AddSingleton<IJwtHelper, JwtHelper>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IJobService, JobService>();
            builder.Services.AddScoped<IJobSeekerService, JobSeekerService>();
            builder.Services.AddScoped<IRecruiterService, RecruiterService>();

            // 7. Register Validators
            builder.Services.AddScoped<IValidator<JobSeekerSignupDto>, JobSeekerSignupValidator>();
            builder.Services.AddScoped<IValidator<RecruiterSignupDto>, RecruiterSignupValidator>();
            builder.Services.AddScoped<IValidator<LoginDto>, LoginValidator>();
            builder.Services.AddScoped<IValidator<RecruiterUpgradeDto>, RecruiterUpgradeValidator>();
            builder.Services.AddScoped<IValidator<JobCreateUpdateDto>, JobCreateUpdateValidator>();
            builder.Services.AddScoped<IValidator<JobSeekerProfileUpdateDto>, JobSeekerProfileUpdateValidator>();
            builder.Services.AddScoped<IValidator<RecruiterProfileUpdateDto>, RecruiterProfileUpdateValidator>();
            builder.Services.AddScoped<IValidator<ApplicationCreateDto>, ApplicationCreateValidator>();
            builder.Services.AddScoped<IValidator<ApplicationStatusDto>, ApplicationStatusValidator>();

            // 8. Register CORS Policy
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    var origins = envConfig.CorsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                        .Select(o => o.Trim())
                                                        .ToArray();
                    policy.WithOrigins(origins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            // 8b. JSON: camelCase property names + omit nulls — matches Node.js natural JS casing.
            // Without this, C# defaults to PascalCase (e.g. "Roles" instead of "roles"),
            // which breaks the React frontend's role detection and hides nav bar items.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    // Serialize enums as strings ("JOB_SEEKER") not integers (0)
                    // Required so the React frontend's hasRole('JOB_SEEKER') check works correctly
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            // 9. Response compression (gzip) — mirrors Node.js `compression` middleware in prod
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
            });

            // 10. Rate limiting — mirrors Node.js express-rate-limit config:
            //   - auth-limiter:   10 requests per 15 minutes (login, refresh)
            //   - signup-limiter:  5 requests per 60 minutes (signup endpoints)
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;

                options.AddFixedWindowLimiter("auth-limiter", cfg =>
                {
                    cfg.PermitLimit = 10;
                    cfg.Window = TimeSpan.FromMinutes(15);
                    cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    cfg.QueueLimit = 0;
                });

                options.AddFixedWindowLimiter("signup-limiter", cfg =>
                {
                    cfg.PermitLimit = 5;
                    cfg.Window = TimeSpan.FromHours(1);
                    cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    cfg.QueueLimit = 0;
                });

                // Custom rejection response body: { "error": "Too many attempts..." }
                options.OnRejected = async (ctx, _) =>
                {
                    ctx.HttpContext.Response.ContentType = "application/json";
                    await ctx.HttpContext.Response.WriteAsync(
                        "{\"error\":\"Too many attempts, please try again later.\"}");
                };
            });

            var app = builder.Build();

            // 9. CLI database seeding mode support
            if (args.Contains("--seed"))
            {
                Console.WriteLine("🌱 Seeding requested via CLI...");
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<JobFinderDbContext>();
                await SeedData.InitializeAsync(context);
                Console.WriteLine("🌱 Seeding finished. Exiting...");
                return;
            }

            // 10. Apply pending EF Core migrations automatically on startup (idempotent)
            try
            {
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<JobFinderDbContext>();
                await SeedData.EnsureDatabaseSchemaAsync(context);
                Console.WriteLine("✅ Database schema initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database schema initialization failed: {ex.Message}");
            }

            // 11. Middlewares setup
            app.UseMiddleware<ErrorHandlerMiddleware>();
            app.UseCors("CorsPolicy");
            app.UseResponseCompression();
            app.UseRateLimiter();

            // Security headers — mirrors Node.js helmet middleware
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                await next();
            });

            app.UseMiddleware<AuthMiddleware>();
            app.MapControllers();

            Console.WriteLine($"🚀 Server is running on port {envConfig.Port} in {app.Environment.EnvironmentName} mode.");
            await app.RunAsync();
        }

        private static void LoadEnvFile(string path)
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    // Only set if not already set — shell/Docker env vars take priority over .env file
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, parts[1].Trim());
                    }
                }
            }
        }
    }
}
