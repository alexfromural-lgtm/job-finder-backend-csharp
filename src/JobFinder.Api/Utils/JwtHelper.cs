using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Config;

namespace JobFinder.Api.Utils
{
    public interface IJwtHelper
    {
        string GenerateAccessToken(string userId, IEnumerable<Role> roles);
        string GenerateRefreshToken(string userId, IEnumerable<Role> roles);
        ClaimsPrincipal? VerifyAccessToken(string token);
        ClaimsPrincipal? VerifyRefreshToken(string token);
    }

    public class JwtHelper : IJwtHelper
    {
        private readonly EnvConfig _config;

        public JwtHelper(EnvConfig config)
        {
            _config = config;
        }

        public string GenerateAccessToken(string userId, IEnumerable<Role> roles)
        {
            return GenerateToken(userId, roles, _config.AccessTokenSecret, TimeSpan.FromMinutes(_config.AccessTokenExpirationMinutes));
        }

        public string GenerateRefreshToken(string userId, IEnumerable<Role> roles)
        {
            return GenerateToken(userId, roles, _config.RefreshTokenSecret, TimeSpan.FromDays(_config.RefreshTokenExpirationDays));
        }

        public ClaimsPrincipal? VerifyAccessToken(string token)
        {
            return VerifyToken(token, _config.AccessTokenSecret);
        }

        public ClaimsPrincipal? VerifyRefreshToken(string token)
        {
            return VerifyToken(token, _config.RefreshTokenSecret);
        }

        private string GenerateToken(string userId, IEnumerable<Role> roles, string secret, TimeSpan expiration)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);

            var claims = new List<Claim>
            {
                new Claim("userId", userId)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim("roles", role.ToString()));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.Add(expiration),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private ClaimsPrincipal? VerifyToken(string token, string secret)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    RoleClaimType = "roles",
                    ClockSkew = TimeSpan.Zero // Immediate expiration check
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
