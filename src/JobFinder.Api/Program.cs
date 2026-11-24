// ============================================================
// FILE: Program.cs
// CONCEPT: Application entry point & dependency injection setup
// ------------------------------------------------------------
// Program.cs is where the entire application is wired together.
// It runs once at startup and does three things:
//
//   1. CONFIGURE SERVICES — Register everything in the DI container
//      (databases, services, middleware, validators, etc.)
//
//   2. BUILD THE APP — WebApplication.Build() finalises the DI
//      container and creates the ASP.NET Core app.
//
//   3. CONFIGURE THE PIPELINE — Add middleware in order and start
//      listening for HTTP requests.
//
// Think of it as the "main() function" of a web application.
// ============================================================

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
// 📌 CONCEPT: Alias import — avoids name collision
//    Both StackExchange.Redis and our models have something named "Role".
//    "using AppModels = ..." lets us write AppModels.Role to be unambiguous.
using AppModels = JobFinder.Api.Common.Models;

namespace JobFinder.Api
{
    public class Program
    {
        // 📌 CONCEPT: async Main — the app entry point is async
        //    "async Task Main" allows us to use "await" at the top level
        //    (e.g. await SeedData.InitializeAsync(...), await app.RunAsync()).
        public static async Task Main(string[] args)
        {
            // ════════════════════════════════════════════════
            // STEP 1: Load environment variables from .env file
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: .env file for local development
            //    In Docker or cloud, env vars are injected by the platform.
            //    Locally we store them in a .env file (not committed to git).
            //    LoadEnvFile reads it and calls Environment.SetEnvironmentVariable.
            //
            //    Two paths tried:
            //      1. Four directories up (project root when running from bin/Debug/net9.0/)
            //      2. Next to the executable (useful when published/Docker)
            LoadEnvFile(Path.Combine(AppContext.BaseDirectory, "../../../../.env"));
            LoadEnvFile(Path.Combine(AppContext.BaseDirectory, ".env"));

            // 📌 CONCEPT: WebApplication.CreateBuilder(args)
            //    Creates the "builder" which lets you configure services
            //    BEFORE the app is built.  "args" passes command-line arguments
            //    so the app can read them (e.g. "--seed" for DB seeding below).
            var builder = WebApplication.CreateBuilder(args);

            // ════════════════════════════════════════════════
            // STEP 2: Read and validate configuration
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: int.TryParse — safe string-to-int conversion
            //    Returns false if the string is not a valid integer.
            //    The ternary "condition ? valueIfTrue : valueIfFalse" picks the default.
            //
            //    EXAMPLE:
            //      PORT="8080"  → int.TryParse succeeds → Port = 8080
            //      PORT=""      → int.TryParse fails   → Port = 5002 (default)
            //      (not set)    → GetEnvironmentVariable returns null
            //                   → int.TryParse(null, ...) fails → Port = 5002
            //
            //    "??" is the null-coalescing operator: a ?? b = "a if a is not null, else b"
            var envConfig = new EnvConfig
            {
                Port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port) ? port : 5002,
                DatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection")   // fallback: appsettings.json
                    ?? string.Empty,
                RedisUrl = Environment.GetEnvironmentVariable("REDIS_URL")
                    ?? builder.Configuration.GetSection("Redis")["ConnectionString"]
                    ?? string.Empty,
                AccessTokenSecret = Environment.GetEnvironmentVariable("ACCESS_TOKEN_SECRET")
                    ?? builder.Configuration.GetSection("Jwt")["AccessTokenSecret"]
                    ?? string.Empty,
                RefreshTokenSecret = Environment.GetEnvironmentVariable("REFRESH_TOKEN_SECRET")
                    ?? builder.Configuration.GetSection("Jwt")["RefreshTokenSecret"]
                    ?? string.Empty,
                AccessTokenExpirationMinutes = int.TryParse(Environment.GetEnvironmentVariable("ACCESS_TOKEN_EXPIRES_IN_MINUTES"), out var accMin) ? accMin : 15,
                RefreshTokenExpirationDays = int.TryParse(Environment.GetEnvironmentVariable("REFRESH_TOKEN_EXPIRES_IN_DAYS"), out var refDays) ? refDays : 7,
                CorsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGIN")
                    ?? builder.Configuration.GetSection("Cors")["AllowedOrigins"]
                    ?? "http://localhost:3000",
                QueueConcurrency = int.TryParse(Environment.GetEnvironmentVariable("QUEUE_CONCURRENCY"), out var concurrency) ? concurrency : 5
            };

            // Throws InvalidOperationException with a clear message if required config is missing.
            envConfig.Validate();

            // 📌 CONCEPT: AddSingleton<T>(instance)
            //    Register a specific INSTANCE as a singleton.
            //    Every service that requests EnvConfig will receive THIS object.
            builder.Services.AddSingleton(envConfig);

            // ════════════════════════════════════════════════
            // STEP 3: Configure Kestrel (the built-in web server)
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: ConfigureKestrel — tell the server which port to listen on
            //    ListenAnyIP listens on all network interfaces (0.0.0.0:PORT).
            //    Important for Docker containers where the IP is dynamic.
            //
            //    EXAMPLE: envConfig.Port = 5002
            //      → Server listens on http://0.0.0.0:5002
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(envConfig.Port);
            });

            // ════════════════════════════════════════════════
            // STEP 4: Register EF Core with PostgreSQL (Npgsql)
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: AddDbContext<T>
            //    Registers JobFinderDbContext as a SCOPED service (new instance per request).
            //    "UseNpgsql" tells EF to use PostgreSQL as the database.
            //
            //    MapEnum — Npgsql needs to know about C# enums so it can translate them
            //    to/from PostgreSQL enum column types.  Without this, EF would store
            //    integers instead of human-readable labels like "job_seeker".
            //
            //    NpgsqlPrismaNameTranslator ensures enum type names and labels match
            //    the ones Prisma originally created (e.g. "Role" type, "job_seeker" label).
            var prismaTranslator = NpgsqlPrismaNameTranslator.Instance;
            builder.Services.AddDbContext<JobFinderDbContext>(options =>
                options.UseNpgsql(envConfig.DatabaseUrl, npgsqlOptions =>
                {
                    npgsqlOptions.MapEnum<AppModels.Role>(nameTranslator: prismaTranslator);
                    npgsqlOptions.MapEnum<AppModels.ApplicationStatus>(nameTranslator: prismaTranslator);
                    npgsqlOptions.MapEnum<AppModels.NotificationType>(nameTranslator: prismaTranslator);
                    npgsqlOptions.MapEnum<AppModels.ReportStatus>(nameTranslator: prismaTranslator);
                }));

            // ════════════════════════════════════════════════
            // STEP 5: Register Redis connection
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: AddSingleton<IInterface>(factory lambda)
            //    "sp =>" is the DI service provider — allows resolving other services.
            //    IConnectionMultiplexer is a heavyweight connection pool — always singleton.
            //    IRedisQueue wraps it in a convenient API — also singleton.
            //
            //    EXAMPLE: envConfig.RedisUrl = "localhost:6379"
            //      → ConnectionMultiplexer.Connect("localhost:6379")
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(envConfig.RedisUrl));
            builder.Services.AddSingleton<IRedisQueue, RedisQueue>();

            // ════════════════════════════════════════════════
            // STEP 6: Register background queue worker
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: AddHostedService<T>
            //    Registers a background service that starts automatically.
            //    QueueWorker inherits from BackgroundService and runs its
            //    ExecuteAsync loop for the entire lifetime of the application.
            builder.Services.AddHostedService<QueueWorker>();

            // ════════════════════════════════════════════════
            // STEP 7: Register business services
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: Service lifetimes
            //    Singleton  → ONE instance for the entire app lifetime
            //      (JwtHelper has no state/request-specific data)
            //    Scoped     → ONE instance per HTTP request
            //      (AuthService uses DbContext which is also scoped)
            //    Transient  → NEW instance every time it's requested
            //      (not used here)
            //
            //    AddSingleton<Interface, Implementation>() means:
            //      "whenever a class asks for IAuthService, give it an AuthService"
            builder.Services.AddSingleton<IJwtHelper, JwtHelper>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IJobService, JobService>();
            builder.Services.AddScoped<IJobSeekerService, JobSeekerService>();
            builder.Services.AddScoped<IRecruiterService, RecruiterService>();

            // ════════════════════════════════════════════════
            // STEP 8: Register FluentValidation validators
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: Registering validators as IValidator<T>
            //    Controllers inject IValidator<JobSeekerSignupDto> etc.
            //    AddScoped = one validator per request (safe, cheap to create).
            //
            //    EXAMPLE usage in a controller:
            //      private readonly IValidator<LoginDto> _loginValidator;
            //      var result = await _loginValidator.ValidateAsync(dto);
            builder.Services.AddScoped<IValidator<JobSeekerSignupDto>, JobSeekerSignupValidator>();
            builder.Services.AddScoped<IValidator<RecruiterSignupDto>, RecruiterSignupValidator>();
            builder.Services.AddScoped<IValidator<LoginDto>, LoginValidator>();
            builder.Services.AddScoped<IValidator<RecruiterUpgradeDto>, RecruiterUpgradeValidator>();
            builder.Services.AddScoped<IValidator<JobCreateUpdateDto>, JobCreateUpdateValidator>();
            builder.Services.AddScoped<IValidator<JobSeekerProfileUpdateDto>, JobSeekerProfileUpdateValidator>();
            builder.Services.AddScoped<IValidator<RecruiterProfileUpdateDto>, RecruiterProfileUpdateValidator>();
            builder.Services.AddScoped<IValidator<ApplicationCreateDto>, ApplicationCreateValidator>();
            builder.Services.AddScoped<IValidator<ApplicationStatusDto>, ApplicationStatusValidator>();

            // ════════════════════════════════════════════════
            // STEP 9: Configure CORS (Cross-Origin Resource Sharing)
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: CORS
            //    Browsers block AJAX requests from one origin (e.g. http://localhost:3000)
            //    to a different origin (http://localhost:5002) unless the SERVER
            //    explicitly allows it via CORS headers.
            //
            //    "CorsPolicy" is the policy name we reference later with app.UseCors("CorsPolicy").
            //
            //    AllowCredentials() is required for cookies to be sent cross-origin.
            //    (Browsers strip cookies from cross-origin requests by default.)
            //
            //    EXAMPLE .env:
            //      CORS_ORIGIN=http://localhost:3000,https://myjobsite.com
            //    Result: requests from both origins are allowed.
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    // Split comma-separated origins and trim whitespace.
                    var origins = envConfig.CorsOrigins
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.Trim())
                        .ToArray();

                    policy.WithOrigins(origins)      // allow these specific origins
                          .AllowAnyMethod()           // GET, POST, PUT, DELETE, etc.
                          .AllowAnyHeader()           // Content-Type, Authorization, etc.
                          .AllowCredentials();        // allow cookies
                });
            });

            // ════════════════════════════════════════════════
            // STEP 10: Configure JSON serialisation
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: JSON naming policy
            //    C# properties are PascalCase (UserName), but JavaScript expects
            //    camelCase (userName).  CamelCase policy auto-converts on serialize.
            //
            //    EXAMPLE:
            //      C# property: public string CompanyName { get; set; }
            //      JSON output: { "companyName": "ACME Corp" }   ← camelCase
            //
            //    WhenWritingNull = omit null properties from the JSON response.
            //    EXAMPLE: salaryRange is null → it won't appear in the JSON at all.
            //
            //    JsonStringEnumConverter → serialize enums as strings not integers.
            //    EXAMPLE: Role.JOB_SEEKER → "JOB_SEEKER" (not "0")
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            // ════════════════════════════════════════════════
            // STEP 11: Response compression (Gzip)
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: Gzip compression
            //    Large JSON responses (e.g. lists of jobs) are compressed before
            //    being sent to the browser.  Browsers automatically decompress them.
            //    This reduces bandwidth and speeds up API calls significantly.
            //    EnableForHttps = true → also compress on HTTPS connections.
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
            });

            // ════════════════════════════════════════════════
            // STEP 12: Rate limiting
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: Rate limiting — throttle abusive clients
            //    Prevents brute-force attacks and API abuse by limiting
            //    how many requests a client can make in a time window.
            //
            //    RejectionStatusCode = 429 is the standard "Too Many Requests" code.
            //
            //    "auth-limiter":   10 requests / 15 minutes  (login, token refresh)
            //    "signup-limiter":  5 requests / 1 hour       (registration)
            //
            //    Fixed window means the counter resets completely at the end of
            //    each window (as opposed to a sliding/token-bucket algorithm).
            //
            //    EXAMPLE: client tries to login 11 times in 15 minutes
            //      → first 10 → HTTP 200 (success) or 401 (wrong password)
            //      → 11th attempt within 15 minutes → HTTP 429
            //         body: {"error":"Too many attempts, please try again later."}
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;

                options.AddFixedWindowLimiter("auth-limiter", cfg =>
                {
                    cfg.PermitLimit = 10;
                    cfg.Window = TimeSpan.FromMinutes(15);
                    cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    cfg.QueueLimit = 0;  // no queuing — reject immediately when over limit
                });

                options.AddFixedWindowLimiter("signup-limiter", cfg =>
                {
                    cfg.PermitLimit = 5;
                    cfg.Window = TimeSpan.FromHours(1);
                    cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    cfg.QueueLimit = 0;
                });

                // Custom rejection response (instead of an empty 429).
                options.OnRejected = async (ctx, _) =>
                {
                    ctx.HttpContext.Response.ContentType = "application/json";
                    await ctx.HttpContext.Response.WriteAsync(
                        "{\"error\":\"Too many attempts, please try again later.\"}");
                };
            });

            // ════════════════════════════════════════════════
            // BUILD THE APPLICATION
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: builder.Build()
            //    Finalises the DI container.  After this line you can no longer
            //    register new services.  You can only add middleware to the pipeline.
            var app = builder.Build();

            // ════════════════════════════════════════════════
            // DATABASE SEEDING SUPPORT (--seed flag)
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: CLI mode via args
            //    "dotnet run -- --seed" passes "--seed" as an argument.
            //    args.Contains("--seed") detects it and runs the seeder,
            //    then exits immediately (no HTTP server started).
            //
            //    EXAMPLE usage:
            //      dotnet run -- --seed
            //      → 🌱 Seeding requested via CLI...
            //      → 🌱 Seeding finished. Exiting...
            if (args.Contains("--seed"))
            {
                Console.WriteLine("🌱 Seeding requested via CLI...");
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<JobFinderDbContext>();
                await SeedData.InitializeAsync(context);
                Console.WriteLine("🌱 Seeding finished. Exiting...");
                return;  // 📌 "return" exits Main — app stops here
            }

            // ════════════════════════════════════════════════
            // AUTO-MIGRATE DATABASE ON STARTUP
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: MigrateAsync — apply pending EF Core migrations
            //    Instead of running "dotnet ef database update" manually,
            //    the app applies migrations on every startup.
            //    This is idempotent: already-applied migrations are skipped.
            //    Wrapped in try/catch so a migration failure logs a warning
            //    instead of crashing the entire server.
            //
            //    EXAMPLE: first time you run the app after adding a migration
            //      → EF creates the tables automatically
            //      → "✅ Database schema initialized successfully."
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

            // ════════════════════════════════════════════════
            // CONFIGURE THE REQUEST PIPELINE (middleware order matters!)
            // ════════════════════════════════════════════════

            // 📌 CONCEPT: Middleware order
            //    Middleware runs in the order it's added.
            //    Request: top → bottom
            //    Response: bottom → top (like a stack)
            //
            //    1. ErrorHandler FIRST — wraps everything so it can catch any exception
            //    2. CORS — sets response headers before the controller can respond
            //    3. Compression — compresses the response body
            //    4. RateLimiter — rejects abusive requests early (before expensive processing)
            //    5. Security headers — safety headers on every response
            //    6. AuthMiddleware — reads JWT from cookie, populates User
            //    7. MapControllers — routes requests to the right controller action

            // Catches all exceptions, converts to JSON error response.
            app.UseMiddleware<ErrorHandlerMiddleware>();

            // Sets CORS headers so browsers accept cross-origin responses.
            app.UseCors("CorsPolicy");

            // Compresses the response body with Gzip.
            app.UseResponseCompression();

            // Rejects clients that exceed the rate limit.
            app.UseRateLimiter();

            // 📌 CONCEPT: Inline middleware with app.Use(async (context, next) => { ... })
            //    Quick way to add one-off middleware without a full class.
            //    Security headers protect against common web vulnerabilities:
            //      X-Content-Type-Options: nosniff   → no MIME-type sniffing (prevents XSS)
            //      X-Frame-Options: DENY             → page cannot be iframed (prevents clickjacking)
            //      X-XSS-Protection: 1; mode=block  → old browser XSS filter
            //      Referrer-Policy                   → controls referrer info sent on navigation
            //      Permissions-Policy                → disable camera/mic/geolocation access
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                await next();  // proceed to next middleware
            });

            // Reads JWT from cookie, sets context.User for downstream middleware/controllers.
            app.UseMiddleware<AuthMiddleware>();

            // 📌 CONCEPT: app.MapControllers()
            //    Connects HTTP routes to controller actions based on the
            //    [Route] and [HttpGet/Post/Put/Delete] attributes.
            app.MapControllers();

            Console.WriteLine($"🚀 Server is running on port {envConfig.Port} in {app.Environment.EnvironmentName} mode.");

            // 📌 CONCEPT: app.RunAsync()
            //    Starts the Kestrel web server and blocks until the app stops.
            //    Uses "await" because RunAsync is asynchronous (it listens for
            //    OS shutdown signals like Ctrl+C and waits for in-flight requests).
            await app.RunAsync();
        }

        // ════════════════════════════════════════════════════
        // Helper: Load .env file
        // ════════════════════════════════════════════════════

        // 📌 CONCEPT: Reading a .env file manually
        //    .env files have the format:
        //      KEY=value
        //      # this is a comment
        //      ANOTHER_KEY=another value
        //
        //    We parse each line, split on the FIRST "=" (Split('=', 2)),
        //    and call Environment.SetEnvironmentVariable.
        //
        //    Key design decision: existing env vars take priority over .env.
        //    This means Docker/cloud-injected variables are never overridden.
        //
        //    EXAMPLE .env:
        //      PORT=5002
        //      DATABASE_URL=postgresql://localhost:5432/jobfinder
        //      ACCESS_TOKEN_SECRET=my-32-char-secret-key-here!!
        private static void LoadEnvFile(string path)
        {
            // 📌 If the file doesn't exist, just return — no error.
            if (!File.Exists(path)) return;

            foreach (var line in File.ReadAllLines(path))
            {
                // Skip empty lines and comments (lines starting with "#").
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                // 📌 CONCEPT: Split('=', 2) — split into at most 2 parts
                //    If the value itself contains "=", we don't want to split on those.
                //    EXAMPLE: "SECRET=abc=def" → ["SECRET", "abc=def"]  ← correct!
                //              Without the "2": ["SECRET", "abc", "def"] ← wrong!
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();

                    // Only set if not already set — env vars from the OS/Docker take priority.
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, parts[1].Trim());
                    }
                }
            }
        }
    }
}
