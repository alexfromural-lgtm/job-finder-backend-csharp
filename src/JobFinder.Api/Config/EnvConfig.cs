// ============================================================
// FILE: Config/EnvConfig.cs
// CONCEPT: Strongly-typed configuration / Settings object
// ------------------------------------------------------------
// Instead of calling Environment.GetEnvironmentVariable("PORT")
// scattered all over the codebase, we read all env vars once
// in Program.cs, store them in this class, and register it as
// a singleton in the DI container.
//
// Every service/controller that needs a setting just declares
// "EnvConfig config" in its constructor — DI injects it
// automatically.
// ============================================================

using System;

namespace JobFinder.Api.Config
{
    // 📌 CONCEPT: Plain C# class as a "settings bag"
    //    All properties have sensible defaults so the app
    //    still starts in a minimal local dev setup.
    public class EnvConfig
    {
        // 📌 CONCEPT: Property with default value
        //    "{ get; set; } = 5002" gives a fallback if PORT is not set.
        //
        //    EXAMPLE in .env:
        //      PORT=8080
        //    → App listens on :8080.  Without the line → listens on :5002.
        public int Port { get; set; } = 5002;

        // Connection string for PostgreSQL.
        // Format: "Host=localhost;Port=5432;Database=jobfinder;Username=postgres;Password=secret"
        // or the shorthand postgres:// URI used by Docker/cloud providers.
        //
        // EXAMPLE:
        //   DATABASE_URL=postgresql://postgres:secret@localhost:5432/jobfinder
        public string DatabaseUrl { get; set; } = string.Empty;

        // Redis connection string.
        // EXAMPLE:
        //   REDIS_URL=localhost:6379
        public string RedisUrl { get; set; } = string.Empty;

        // 📌 CONCEPT: Two different JWT secrets
        //    Access tokens and refresh tokens are signed with DIFFERENT secrets
        //    so a stolen refresh token cannot be used as an access token.
        //
        //    EXAMPLE:
        //      ACCESS_TOKEN_SECRET=my-super-secret-key-32chars
        //      REFRESH_TOKEN_SECRET=another-different-secret-32chars
        public string AccessTokenSecret { get; set; } = string.Empty;
        public string RefreshTokenSecret { get; set; } = string.Empty;

        // How long tokens stay valid.
        // Access token is short-lived (15 min) so a stolen token expires quickly.
        // Refresh token is long-lived (7 days) for user convenience.
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        public int RefreshTokenExpirationDays { get; set; } = 7;

        // 📌 CONCEPT: Multiple CORS origins as a comma-separated string
        //    EXAMPLE .env value:
        //      CORS_ORIGIN=http://localhost:3000,https://myjobsite.com
        //    Program.cs splits on ',' to get the array of allowed origins.
        public string CorsOrigins { get; set; } = "http://localhost:3000";

        // How many queue jobs can be processed simultaneously.
        public int QueueConcurrency { get; set; } = 5;

        // --------------------------------------------------------
        // 📌 CONCEPT: Guard / validation method called at startup
        //    Fail-fast: if required config is missing the app throws
        //    immediately with a clear message instead of crashing
        //    deep inside a service with a confusing NullReferenceException.
        //
        //    EXAMPLE call in Program.cs:
        //      envConfig.Validate(); // throws if DATABASE_URL is empty
        // --------------------------------------------------------
        public void Validate()
        {
            // 📌 string.IsNullOrWhiteSpace returns true for null, "" and "   "
            if (string.IsNullOrWhiteSpace(DatabaseUrl))
                throw new InvalidOperationException("DATABASE_URL is required.");

            if (string.IsNullOrWhiteSpace(RedisUrl))
                throw new InvalidOperationException("REDIS_URL is required.");

            // Minimum 16-character secret to prevent weak HMAC keys.
            if (string.IsNullOrWhiteSpace(AccessTokenSecret) || AccessTokenSecret.Length < 16)
                throw new InvalidOperationException("ACCESS_TOKEN_SECRET must be at least 16 characters long.");

            if (string.IsNullOrWhiteSpace(RefreshTokenSecret) || RefreshTokenSecret.Length < 16)
                throw new InvalidOperationException("REFRESH_TOKEN_SECRET must be at least 16 characters long.");
        }
    }
}
