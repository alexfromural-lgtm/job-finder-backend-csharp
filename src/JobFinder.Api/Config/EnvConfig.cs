using System;

namespace JobFinder.Api.Config
{
    public class EnvConfig
    {
        public int Port { get; set; } = 5002;
        public string DatabaseUrl { get; set; } = string.Empty;
        public string RedisUrl { get; set; } = string.Empty;
        public string AccessTokenSecret { get; set; } = string.Empty;
        public string RefreshTokenSecret { get; set; } = string.Empty;
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        public int RefreshTokenExpirationDays { get; set; } = 7;
        public string CorsOrigins { get; set; } = "http://localhost:3000";
        public int QueueConcurrency { get; set; } = 5;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DatabaseUrl))
                throw new InvalidOperationException("DATABASE_URL is required.");

            if (string.IsNullOrWhiteSpace(RedisUrl))
                throw new InvalidOperationException("REDIS_URL is required.");

            if (string.IsNullOrWhiteSpace(AccessTokenSecret) || AccessTokenSecret.Length < 16)
                throw new InvalidOperationException("ACCESS_TOKEN_SECRET must be at least 16 characters long.");

            if (string.IsNullOrWhiteSpace(RefreshTokenSecret) || RefreshTokenSecret.Length < 16)
                throw new InvalidOperationException("REFRESH_TOKEN_SECRET must be at least 16 characters long.");
        }
    }
}
