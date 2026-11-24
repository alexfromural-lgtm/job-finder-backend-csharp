// ============================================================
// FILE: Utils/JwtHelper.cs
// CONCEPT: JSON Web Tokens (JWT) — generation & verification
// ------------------------------------------------------------
// A JWT is a compact, URL-safe string that proves "this user
// is authenticated" without hitting the database on every
// request.  It has three base64-encoded parts:
//
//   Header.Payload.Signature
//
//   Header   → algorithm used (HS256 here)
//   Payload  → claims (userId, roles, expiry)
//   Signature → HMAC of the first two parts using the secret key
//
// Anyone can DECODE a JWT, but only the server can CREATE a valid
// SIGNATURE — that's the security guarantee.
// ============================================================

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
    // 📌 CONCEPT: Interface + implementation pattern
    //    The interface declares WHAT the helper does.
    //    The class defines HOW it does it.
    //    Controllers and services depend only on the interface,
    //    so unit tests can inject a fake/mock implementation.
    //
    //    EXAMPLE:
    //      // Real app: DI injects JwtHelper
    //      builder.Services.AddSingleton<IJwtHelper, JwtHelper>();
    //
    //      // Unit test: inject a mock that always returns "fake-token"
    //      var mockJwt = new Mock<IJwtHelper>();
    //      mockJwt.Setup(j => j.GenerateAccessToken(...)).Returns("fake-token");
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

        // 📌 CONCEPT: Constructor injection
        //    The DI container supplies EnvConfig when it creates JwtHelper.
        //    We don't use "new EnvConfig()" here — that would couple
        //    JwtHelper to config creation and make testing harder.
        public JwtHelper(EnvConfig config)
        {
            _config = config;
        }

        // ────────────────────────────────────────────────────
        // Public convenience methods — thin wrappers around
        // the private GenerateToken / VerifyToken helpers.
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Access token vs Refresh token
        //    Access token  → SHORT life (15 min). Sent on every API call.
        //    Refresh token → LONG life (7 days). Used ONLY to get a new access token.
        //
        //    EXAMPLE flow:
        //    1. User logs in → server returns both tokens in HttpOnly cookies.
        //    2. Browser sends access token automatically with each request.
        //    3. After 15 min, access token expires → 401 Unauthorized.
        //    4. Frontend calls POST /api/auth/refresh → server verifies the
        //       refresh token and issues fresh tokens.

        public string GenerateAccessToken(string userId, IEnumerable<Role> roles)
        {
            return GenerateToken(
                userId,
                roles,
                _config.AccessTokenSecret,
                TimeSpan.FromMinutes(_config.AccessTokenExpirationMinutes));
        }

        public string GenerateRefreshToken(string userId, IEnumerable<Role> roles)
        {
            return GenerateToken(
                userId,
                roles,
                _config.RefreshTokenSecret,
                TimeSpan.FromDays(_config.RefreshTokenExpirationDays));
        }

        public ClaimsPrincipal? VerifyAccessToken(string token)
        {
            return VerifyToken(token, _config.AccessTokenSecret);
        }

        public ClaimsPrincipal? VerifyRefreshToken(string token)
        {
            return VerifyToken(token, _config.RefreshTokenSecret);
        }

        // ────────────────────────────────────────────────────
        // Private helpers
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: JWT generation step by step
        //
        //    EXAMPLE result for userId="u1", roles=[JOB_SEEKER]:
        //      eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9   ← header (algorithm)
        //      .eyJ1c2VySWQiOiJ1MSIsInJvbGVzIjoiSk9CX1NFRUtFUiIsImV4cCI6…  ← payload
        //      .SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c   ← signature
        private string GenerateToken(string userId, IEnumerable<Role> roles, string secret, TimeSpan expiration)
        {
            // JwtSecurityTokenHandler is the .NET class that creates/reads JWTs.
            var tokenHandler = new JwtSecurityTokenHandler();

            // The secret is converted to bytes because HMAC works on raw bytes.
            // UTF-8 encoding ensures consistent byte representation.
            var key = Encoding.UTF8.GetBytes(secret);

            // 📌 CONCEPT: Claims
            //    Claims are key-value pairs embedded in the token payload.
            //    They describe the user WITHOUT requiring a DB lookup.
            //
            //    EXAMPLE claims for user "u1" who is a JOB_SEEKER and RECRUITER:
            //      userId = "u1"
            //      roles  = "JOB_SEEKER"
            //      roles  = "RECRUITER"   ← multiple "roles" claims, one per role
            var claims = new List<Claim>
            {
                new Claim("userId", userId)  // custom claim for our app
            };

            // 📌 CONCEPT: Multiple claims with the same key
            //    A user can have many roles, so we add one "roles" claim per role.
            foreach (var role in roles)
            {
                claims.Add(new Claim("roles", role.ToString()));
            }

            // SecurityTokenDescriptor bundles all token settings together.
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                // ClaimsIdentity wraps our claims list in a standard .NET type.
                Subject = new ClaimsIdentity(claims),

                // When does the token become invalid?
                // DateTime.UtcNow + expiration (e.g. +15 minutes for access tokens)
                Expires = DateTime.UtcNow.Add(expiration),

                // 📌 CONCEPT: Signing credentials
                //    SymmetricSecurityKey uses the SAME key to sign and verify.
                //    HmacSha256Signature is an industry-standard algorithm for JWTs.
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            // Build the token object and serialise it to the compact string form.
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // 📌 CONCEPT: JWT verification — parse and validate the signature + expiry
        //
        //    Returns:
        //      ClaimsPrincipal → token is valid; claims are accessible via principal.FindFirst(...)
        //      null            → token is invalid, expired, or tampered with
        //
        //    EXAMPLE caller:
        //      var principal = jwtHelper.VerifyAccessToken(token);
        //      var userId = principal?.FindFirst("userId")?.Value; // "u1"
        private ClaimsPrincipal? VerifyToken(string token, string secret)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    // Ensure the signature was made with OUR key.
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    // We don't use "iss" (issuer) or "aud" (audience) claims,
                    // so we skip those validations.
                    ValidateIssuer = false,
                    ValidateAudience = false,

                    // 📌 CONCEPT: RoleClaimType
                    //    Tells .NET that "roles" is the claim key for roles.
                    //    This makes User.IsInRole("RECRUITER") work correctly.
                    RoleClaimType = "roles",

                    // 📌 CONCEPT: ClockSkew
                    //    Small tolerance (30 seconds) for clock differences
                    //    between the server that ISSUED the token and the
                    //    server that VERIFIES it (e.g. in a multi-server setup).
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // ValidateToken throws if the token is invalid/expired.
                // On success it returns a ClaimsPrincipal (the "user").
                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                // 📌 CONCEPT: Swallow validation exceptions → return null
                //    We deliberately catch all exceptions here because
                //    an invalid token is a normal (non-exceptional) event —
                //    an anonymous visitor will always have no/bad token.
                return null;
            }
        }
    }
}
