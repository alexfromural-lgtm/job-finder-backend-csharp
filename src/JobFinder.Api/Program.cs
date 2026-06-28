using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using FluentValidation;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Config;
using JobFinder.Api.Data;
using JobFinder.Api.Middleware;
using JobFinder.Api.Queue;
using JobFinder.Api.Services;
using JobFinder.Api.Utils;
using JobFinder.Api.Validators;

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

            // 3. Register EF Core DbContext with Npgsql & snake_case naming (matches Prisma schema conventions)
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(envConfig.DatabaseUrl);
            // Register Npgsql enum mappings with explicit PostgreSQL names matching Prisma schema
            dataSourceBuilder.MapEnum<Role>("Role");
            dataSourceBuilder.MapEnum<ApplicationStatus>("ApplicationStatus");
            dataSourceBuilder.MapEnum<NotificationType>("NotificationType");
            dataSourceBuilder.MapEnum<ReportStatus>("ReportStatus");
            var dataSource = dataSourceBuilder.Build();

            builder.Services.AddDbContext<JobFinderDbContext>(options =>
                options.UseNpgsql(dataSource)
                       .UseSnakeCaseNamingConventions());

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

            builder.Services.AddControllers();

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

            // 10. Automatically run migrations on normal startup for ease of development
            try
            {
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<JobFinderDbContext>();
                await context.Database.MigrateAsync();
                Console.WriteLine("✅ Database migrated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Database migration on startup failed: {ex.Message}");
            }

            // 11. Middlewares setup
            app.UseMiddleware<ErrorHandlerMiddleware>();
            app.UseCors("CorsPolicy");
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
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                }
            }
        }
    }
}
