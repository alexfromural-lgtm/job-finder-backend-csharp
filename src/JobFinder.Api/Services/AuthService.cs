// ============================================================
// FILE: Services/AuthService.cs
// CONCEPT: Service layer — business logic, separated from HTTP
// ------------------------------------------------------------
// Controllers are thin: they validate input and call services.
// Services hold all the real logic: DB queries, password hashing,
// token generation, business rules.
//
// Benefits of this separation:
//   ✔ Services are testable without HTTP context.
//   ✔ Multiple controllers can share the same service.
//   ✔ Swapping the HTTP framework won't break business logic.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JobFinder.Api.Common.Exceptions;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Data;
using JobFinder.Api.Data.Entities;
using JobFinder.Api.Utils;

namespace JobFinder.Api.Services
{
    // 📌 CONCEPT: Interface definition for the service
    //    Controllers depend on IAuthService, NOT on AuthService directly.
    //    This enables:
    //      • Unit testing with mock implementations
    //      • Swapping the implementation without changing callers
    //
    //    EXAMPLE: replace AuthService with an OAuth2-based one:
    //      builder.Services.AddScoped<IAuthService, OAuth2AuthService>();
    //      // → no changes in AuthController needed!
    public interface IAuthService
    {
        Task<TokenResultDto> SignupJobSeekerAsync(JobSeekerSignupDto dto);
        Task<TokenResultDto> SignupRecruiterAsync(RecruiterSignupDto dto);
        Task<(UserResponseDto User, TokenResultDto Tokens)> UpgradeToRecruiterAsync(string userId, RecruiterUpgradeDto dto);
        Task<TokenResultDto> LoginAsync(LoginDto dto);
        Task<TokenResultDto> RefreshTokensAsync(string refreshToken);
        Task<UserResponseDto> GetCurrentUserAsync(string userId);
    }

    // 📌 CONCEPT: Scoped lifetime
    //    "AddScoped<IAuthService, AuthService>()" means one AuthService
    //    instance per HTTP request.  It's re-created for each request so
    //    the DbContext (also scoped) is freshly injected each time.
    public class AuthService : IAuthService
    {
        private readonly JobFinderDbContext _context;
        private readonly IJwtHelper _jwtHelper;

        // 📌 CONCEPT: Constructor injection
        //    ASP.NET Core DI container automatically provides
        //    JobFinderDbContext and IJwtHelper when creating AuthService.
        public AuthService(JobFinderDbContext context, IJwtHelper jwtHelper)
        {
            _context = context;
            _jwtHelper = jwtHelper;
        }

        // ────────────────────────────────────────────────────
        // SIGNUP — Job Seeker
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: async/await
        //    "async Task<T>" declares an asynchronous method.
        //    "await" suspends the method while waiting for a DB/IO result,
        //    freeing the thread to handle other requests.
        //
        //    EXAMPLE call chain:
        //      POST /api/auth/signup/jobseeker
        //        → AuthController.SignupJobSeeker()   (validates input)
        //        → AuthService.SignupJobSeekerAsync() (this method)
        //        → DB INSERT + JWT generation
        //        → returns TokenResultDto
        public async Task<TokenResultDto> SignupJobSeekerAsync(JobSeekerSignupDto dto)
        {
            // Basic guard — extra safety beyond FluentValidation.
            ValidateCredentials(dto.Email, dto.Password);

            // Check no other user has this email (async DB query).
            await CheckUserExistsByEmailAsync(dto.Email);

            // 📌 CONCEPT: BCrypt password hashing
            //    BCrypt.HashPassword("secret", 10) produces a salted hash.
            //    "10" is the work factor (cost): higher = slower hashing = harder to brute-force.
            //    The salt is embedded in the hash string itself.
            //
            //    EXAMPLE:
            //      Input:  "secret123"
            //      Output: "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy"
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 10);

            // Create the entity — EF will generate a UUID for Id automatically.
            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = passwordHash,
                Roles = new List<Role> { Role.JOB_SEEKER },
                // 📌 Navigation property: creates the related row in JobSeekerProfiles
                //    automatically in the same SaveChangesAsync call.
                JobSeeker = new JobSeekerProfile()
            };

            // Queue the INSERT but don't execute yet.
            _context.Users.Add(user);

            // Execute all pending SQL in a transaction.
            // Returns number of rows affected (we don't use it here).
            await _context.SaveChangesAsync();

            return GenerateTokensForUser(user.Id, user.Roles);
        }

        // ────────────────────────────────────────────────────
        // SIGNUP — Recruiter
        // ────────────────────────────────────────────────────
        public async Task<TokenResultDto> SignupRecruiterAsync(RecruiterSignupDto dto)
        {
            ValidateCredentials(dto.Email, dto.Password);
            await CheckUserExistsByEmailAsync(dto.Email);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 10);

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = passwordHash,
                Roles = new List<Role> { Role.RECRUITER },
                // 📌 Recruiter gets a RecruiterProfile — different from JobSeekerProfile.
                Recruiter = new RecruiterProfile
                {
                    CompanyName = dto.CompanyName,
                    CompanyWebsite = dto.CompanyWebsite,
                    Description = dto.Description,
                    Industry = dto.Industry
                }
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return GenerateTokensForUser(user.Id, user.Roles);
        }

        // ────────────────────────────────────────────────────
        // UPGRADE: Job Seeker → also becomes a Recruiter
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Tuple return type "(UserResponseDto User, TokenResultDto Tokens)"
        //    C# 7+ allows returning multiple values as a named tuple.
        //    Caller deconstructs it:
        //      var (user, tokens) = await _authService.UpgradeToRecruiterAsync(id, dto);
        public async Task<(UserResponseDto User, TokenResultDto Tokens)> UpgradeToRecruiterAsync(string userId, RecruiterUpgradeDto dto)
        {
            // 📌 CONCEPT: .Include() — eager loading of related data
            //    Without Include, user.Recruiter would always be null.
            //    Include performs a SQL JOIN to load the related row.
            var user = await _context.Users
                .Include(u => u.Recruiter)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) throw new AppException("User not found", 404);

            // Business rules — throw if the upgrade is not allowed.
            if (!user.Roles.Contains(Role.JOB_SEEKER))
                throw new AppException("Only Job Seekers can upgrade to Recruiter", 403);

            if (user.Recruiter != null)
                throw new AppException("User already has a recruiter profile", 409);

            // Add the new role (the existing JOB_SEEKER role is kept).
            user.Roles.Add(Role.RECRUITER);

            // Create the RecruiterProfile — EF will INSERT it and link via UserId.
            user.Recruiter = new RecruiterProfile
            {
                CompanyName = dto.CompanyName,
                CompanyWebsite = dto.CompanyWebsite,
                Description = dto.Description,
                Industry = dto.Industry
            };

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var tokens = GenerateTokensForUser(user.Id, user.Roles);

            // 📌 CONCEPT: Mapping entity → DTO to avoid exposing Password etc.
            var userDto = new UserResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Roles = user.Roles,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            return (userDto, tokens);
        }

        // ────────────────────────────────────────────────────
        // LOGIN
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Generic error message for security
        //    Both "user not found" and "wrong password" return the SAME message
        //    "Invalid email or password".  Giving different messages would let
        //    an attacker enumerate which emails are registered.
        public async Task<TokenResultDto> LoginAsync(LoginDto dto)
        {
            ValidateCredentials(dto.Email, dto.Password);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) throw new AppException("Invalid email or password", 401);
            if (!user.IsActive) throw new AppException("Account is deactivated", 403);

            // 📌 CONCEPT: BCrypt.Verify
            //    Hashes the plain-text password and compares with the stored hash.
            //    BCrypt embeds the salt in the hash, so it re-extracts it automatically.
            //
            //    EXAMPLE:
            //      BCrypt.Verify("secret123", "$2a$10$N9qo8...") → true
            //      BCrypt.Verify("wrongpass", "$2a$10$N9qo8...") → false
            var isValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.Password);
            if (!isValid) throw new AppException("Invalid email or password", 401);

            return GenerateTokensForUser(user.Id, user.Roles);
        }

        // ────────────────────────────────────────────────────
        // TOKEN REFRESH
        // ────────────────────────────────────────────────────
        public async Task<TokenResultDto> RefreshTokensAsync(string refreshToken)
        {
            // 📌 CONCEPT: Verifying a refresh token to get a new access token
            //    1. Validate the refresh token signature + expiry.
            //    2. Extract userId from claims.
            //    3. Reload user from DB (to check IsActive again, get latest roles).
            //    4. Issue a NEW pair of tokens.
            var principal = _jwtHelper.VerifyRefreshToken(refreshToken);
            if (principal == null)
            {
                throw new AppException("Invalid or expired refresh token", 401);
            }

            // "?" = safe navigation — returns null if principal is null (can't happen here but safe).
            var userId = principal.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                throw new AppException("Invalid token payload", 401);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) throw new AppException("User not found", 404);
            if (!user.IsActive) throw new AppException("Account is deactivated", 403);

            return GenerateTokensForUser(user.Id, user.Roles);
        }

        // ────────────────────────────────────────────────────
        // GET CURRENT USER (for /api/auth/me)
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Projection with .Select() — query only needed columns
        //    Instead of loading the full entity (all columns) and then
        //    mapping, we project directly in LINQ.  EF generates a SQL
        //    SELECT with only the columns we reference — faster and safer
        //    (we can never accidentally return the password this way).
        //
        //    EXAMPLE SQL generated:
        //      SELECT id, name, email, roles, "isActive", "createdAt", "updatedAt"
        //      FROM "User"
        //      WHERE id = @userId
        //      LIMIT 1
        public async Task<UserResponseDto> GetCurrentUserAsync(string userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserResponseDto   // ← project to DTO in the query
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Roles = u.Roles,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null) throw new AppException("User not found", 404);
            return user;
        }

        // ────────────────────────────────────────────────────
        // Private helpers (not part of the interface)
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Private method for reusable logic
        //    Keeps the public methods concise; avoids copy-pasting the same check.
        private void ValidateCredentials(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new AppException("Email and password are required", 400);
            }
        }

        // 📌 CONCEPT: AnyAsync — returns bool without loading the full entity
        //    More efficient than FirstOrDefaultAsync for existence checks.
        //
        //    SQL generated: SELECT CASE WHEN EXISTS(SELECT 1 FROM "User" WHERE email = @email)
        //                              THEN 1 ELSE 0 END
        private async Task CheckUserExistsByEmailAsync(string email)
        {
            var exists = await _context.Users.AnyAsync(u => u.Email == email);
            if (exists)
            {
                throw new AppException("User already exists", 409);
            }
        }

        // Central method to generate BOTH access + refresh tokens in one call.
        private TokenResultDto GenerateTokensForUser(string userId, IEnumerable<Role> roles)
        {
            var accessToken = _jwtHelper.GenerateAccessToken(userId, roles);
            var refreshToken = _jwtHelper.GenerateRefreshToken(userId, roles);

            return new TokenResultDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }
    }
}
