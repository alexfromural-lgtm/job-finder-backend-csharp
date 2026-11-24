// ============================================================
// FILE: Controllers/AuthController.cs
// CONCEPT: ASP.NET Core MVC Controller — HTTP layer
// ------------------------------------------------------------
// A Controller is the bridge between HTTP and your business logic.
// It:
//   1. Receives the HTTP request (route, body, cookies, headers).
//   2. Validates input (via FluentValidation).
//   3. Delegates to a Service for business logic.
//   4. Returns an HTTP response (status code + JSON body).
//
// Controllers should NOT contain business logic — keep them thin.
// ============================================================

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FluentValidation;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Config;
using JobFinder.Api.Middleware;
using JobFinder.Api.Services;

namespace JobFinder.Api.Controllers
{
    // 📌 CONCEPT: [ApiController] attribute
    //    Enables automatic model binding (reads JSON body into DTOs),
    //    automatic 400 responses for model validation errors,
    //    and requirement that all params come from explicit sources ([FromBody], etc.)
    [ApiController]

    // 📌 CONCEPT: [Route] attribute — sets the base URL for all actions in this controller
    //    "api/auth" means every action URL starts with /api/auth/...
    //    Action-level [HttpPost("login")] appends to give /api/auth/login
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        // 📌 CONCEPT: Readonly private fields — dependency injection targets
        //    ASP.NET Core DI provides these through the constructor.
        //    Making them readonly ensures they can't be accidentally replaced.
        private readonly IAuthService _authService;
        private readonly EnvConfig _config;
        private readonly IValidator<JobSeekerSignupDto> _jobSeekerSignupValidator;
        private readonly IValidator<RecruiterSignupDto> _recruiterSignupValidator;
        private readonly IValidator<LoginDto> _loginValidator;
        private readonly IValidator<RecruiterUpgradeDto> _recruiterUpgradeValidator;

        // 📌 CONCEPT: Constructor injection
        //    Every parameter is provided automatically by the DI container.
        //    We don't use "new" anywhere — the container manages lifetimes.
        //
        //    EXAMPLE: when a request arrives, ASP.NET Core:
        //      1. Creates an AuthController instance
        //      2. Resolves IAuthService → creates/reuses AuthService
        //      3. Resolves EnvConfig → returns the singleton
        //      4. Resolves each IValidator<T> → creates/reuses the validator
        public AuthController(
            IAuthService authService,
            EnvConfig config,
            IValidator<JobSeekerSignupDto> jobSeekerSignupValidator,
            IValidator<RecruiterSignupDto> recruiterSignupValidator,
            IValidator<LoginDto> loginValidator,
            IValidator<RecruiterUpgradeDto> recruiterUpgradeValidator)
        {
            _authService = authService;
            _config = config;
            _jobSeekerSignupValidator = jobSeekerSignupValidator;
            _recruiterSignupValidator = recruiterSignupValidator;
            _loginValidator = loginValidator;
            _recruiterUpgradeValidator = recruiterUpgradeValidator;
        }

        // ────────────────────────────────────────────────────
        // POST /api/auth/signup/jobseeker
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: [HttpPost("signup/jobseeker")]
        //    Maps this action to HTTP POST requests at the given sub-path.
        //    Full URL: POST http://localhost:5002/api/auth/signup/jobseeker

        // 📌 CONCEPT: [EnableRateLimiting("signup-limiter")]
        //    Applies the "signup-limiter" policy (5 requests / hour per IP)
        //    defined in Program.cs.  Prevents registration spam/abuse.
        [HttpPost("signup/jobseeker")]
        [EnableRateLimiting("signup-limiter")]
        public async Task<IActionResult> SignupJobSeeker([FromBody] JobSeekerSignupDto dto)
        {
            // 📌 CONCEPT: FluentValidation
            //    ValidateAsync runs all rules defined in JobSeekerSignupValidator.
            //    Returns a ValidationResult with IsValid flag and list of errors.
            //
            //    EXAMPLE: dto.Password = "abc" (too short)
            //      validation.IsValid = false
            //      validation.Errors[0].ErrorMessage = "Password must be at least 6 characters"
            var validation = await _jobSeekerSignupValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                // Return first error only — matches the Node.js API behaviour.
                // HTTP 400 Bad Request with { "error": "..." }
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var tokens = await _authService.SignupJobSeekerAsync(dto);

            // Store tokens in HttpOnly cookies (browser can't access via JS → XSS protection).
            SetTokenCookies(tokens);

            // 📌 CONCEPT: StatusCode(201, ...) — 201 Created
            //    Use 201 (not 200) for resource creation to follow REST conventions.
            return StatusCode(201, new { message = "Signed up successfully" });
        }

        // ────────────────────────────────────────────────────
        // POST /api/auth/signup/recruiter
        // ────────────────────────────────────────────────────
        [HttpPost("signup/recruiter")]
        [EnableRateLimiting("signup-limiter")]
        public async Task<IActionResult> SignupRecruiter([FromBody] RecruiterSignupDto dto)
        {
            var validation = await _recruiterSignupValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var tokens = await _authService.SignupRecruiterAsync(dto);
            SetTokenCookies(tokens);
            return StatusCode(201, new { message = "Signed up successfully" });
        }

        // ────────────────────────────────────────────────────
        // POST /api/auth/upgrade/recruiter
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: [AuthorizeRoles(Role.JOB_SEEKER)]
        //    Our custom attribute (see Middleware/AuthorizeRolesAttribute.cs).
        //    Only a user with the JOB_SEEKER role can call this endpoint.
        //    Recruiters who already have both roles are excluded from upgrading again.
        [HttpPost("upgrade/recruiter")]
        [AuthorizeRoles(Role.JOB_SEEKER)]
        public async Task<IActionResult> UpgradeToRecruiter([FromBody] RecruiterUpgradeDto dto)
        {
            var validation = await _recruiterUpgradeValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            // 📌 CONCEPT: Reading the authenticated user's ID from JWT claims
            //    AuthMiddleware already validated the token and set context.User.
            //    User.FindFirst("userId") reads the "userId" claim from the JWT.
            //    "!" (null-forgiving operator): we know userId exists because
            //    [AuthorizeRoles] already blocked unauthenticated requests.
            var userId = User.FindFirst("userId")!.Value;

            // 📌 CONCEPT: Tuple deconstruction
            //    UpgradeToRecruiterAsync returns (UserResponseDto, TokenResultDto).
            //    We destructure it inline:  var (user, tokens) = ...
            var (user, tokens) = await _authService.UpgradeToRecruiterAsync(userId, dto);
            SetTokenCookies(tokens);

            // Return the updated user + both tokens.
            return Ok(new
            {
                user,
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken
            });
        }

        // ────────────────────────────────────────────────────
        // POST /api/auth/login
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: [EnableRateLimiting("auth-limiter")]
        //    10 requests per 15 minutes — throttles brute-force login attempts.
        [HttpPost("login")]
        [EnableRateLimiting("auth-limiter")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var validation = await _loginValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var tokens = await _authService.LoginAsync(dto);
            SetTokenCookies(tokens);

            // 📌 CONCEPT: Ok(...) → HTTP 200 with a JSON body
            //    return Ok(new { message = "..." }) → {"message":"Logged in successfully"}
            return Ok(new { message = "Logged in successfully" });
        }

        // ────────────────────────────────────────────────────
        // POST /api/auth/logout
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Logout by clearing cookies (server-side)
        //    Since tokens are in HttpOnly cookies, the client JS can't delete them.
        //    We overwrite them with empty-value cookies that expire in the past.
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            ClearTokenCookies();
            return Ok(new { message = "Logged out successfully" });
        }

        // ────────────────────────────────────────────────────
        // POST /api/auth/refresh
        // ────────────────────────────────────────────────────
        [HttpPost("refresh")]
        [EnableRateLimiting("auth-limiter")]
        public async Task<IActionResult> Refresh()
        {
            // 📌 CONCEPT: TryGetValue on the cookie collection
            //    Returns false if the cookie is missing, so we can return 401.
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized(new { error = "Refresh token is missing" });
            }

            var tokens = await _authService.RefreshTokensAsync(refreshToken);
            SetTokenCookies(tokens);
            return Ok(new { message = "Token refreshed" });
        }

        // ────────────────────────────────────────────────────
        // GET /api/auth/me
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: [RequireAuth] — authentication required, any role allowed
        //    Returns the current user's profile.
        //    The token was already validated by AuthMiddleware;
        //    [RequireAuth] just blocks unauthenticated requests.
        [HttpGet("me")]
        [RequireAuth]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirst("userId")!.Value;
            var user = await _authService.GetCurrentUserAsync(userId);
            return Ok(new { user });  // { "user": { "id": "...", ... } }
        }

        // ────────────────────────────────────────────────────
        // Private helpers
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: HttpOnly cookies for token storage
        //    HttpOnly = true → JavaScript can't read the cookie (prevents XSS theft).
        //    Secure = true   → Cookie is only sent over HTTPS (prevents interception).
        //    SameSite = Lax  → Cookie sent on cross-site top-level navigations but
        //                      NOT on cross-site AJAX → CSRF protection.
        //
        //    EXAMPLE cookie header sent by the browser on subsequent requests:
        //      Cookie: accessToken=eyJ...; refreshToken=eyJ...
        private void SetTokenCookies(TokenResultDto tokens)
        {
            var isProd = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

            var accessOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,          // only HTTPS in production
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddMinutes(_config.AccessTokenExpirationMinutes)
            };

            var refreshOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(_config.RefreshTokenExpirationDays)
            };

            // Response.Cookies.Append → adds Set-Cookie headers to the HTTP response.
            Response.Cookies.Append("accessToken", tokens.AccessToken, accessOptions);
            Response.Cookies.Append("refreshToken", tokens.RefreshToken, refreshOptions);
        }

        // 📌 CONCEPT: "Delete" cookie by setting an expiry in the past
        //    Browsers discard cookies whose expiry is in the past.
        //    This is the standard way to log a user out server-side.
        private void ClearTokenCookies()
        {
            var isProd = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(-1)  // yesterday = already expired
            };
            Response.Cookies.Delete("accessToken", options);
            Response.Cookies.Delete("refreshToken", options);
        }
    }
}
