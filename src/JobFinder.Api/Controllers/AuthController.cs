using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Config;
using JobFinder.Api.Middleware;
using JobFinder.Api.Services;

namespace JobFinder.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly EnvConfig _config;
        private readonly IValidator<JobSeekerSignupDto> _jobSeekerSignupValidator;
        private readonly IValidator<RecruiterSignupDto> _recruiterSignupValidator;
        private readonly IValidator<LoginDto> _loginValidator;
        private readonly IValidator<RecruiterUpgradeDto> _recruiterUpgradeValidator;

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

        [HttpPost("signup/jobseeker")]
        public async Task<IActionResult> SignupJobSeeker([FromBody] JobSeekerSignupDto dto)
        {
            var validation = await _jobSeekerSignupValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var tokens = await _authService.SignupJobSeekerAsync(dto);
            SetTokenCookies(tokens);
            return Ok(tokens);
        }

        [HttpPost("signup/recruiter")]
        public async Task<IActionResult> SignupRecruiter([FromBody] RecruiterSignupDto dto)
        {
            var validation = await _recruiterSignupValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var tokens = await _authService.SignupRecruiterAsync(dto);
            SetTokenCookies(tokens);
            return Ok(tokens);
        }

        [HttpPost("upgrade")]
        [AuthorizeRoles(Role.JOB_SEEKER)]
        public async Task<IActionResult> UpgradeToRecruiter([FromBody] RecruiterUpgradeDto dto)
        {
            var validation = await _recruiterUpgradeValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var userId = User.FindFirst("userId")!.Value;
            var (user, tokens) = await _authService.UpgradeToRecruiterAsync(userId, dto);
            SetTokenCookies(tokens);

            return Ok(new
            {
                user,
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var validation = await _loginValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var tokens = await _authService.LoginAsync(dto);
            SetTokenCookies(tokens);
            return Ok(tokens);
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            ClearTokenCookies();
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized(new { error = "Refresh token is missing" });
            }

            var tokens = await _authService.RefreshTokensAsync(refreshToken);
            SetTokenCookies(tokens);
            return Ok(tokens);
        }

        [HttpGet("me")]
        [RequireAuth]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirst("userId")!.Value;
            var user = await _authService.GetCurrentUserAsync(userId);
            return Ok(new { user });
        }

        private void SetTokenCookies(TokenResultDto tokens)
        {
            var isProd = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

            var accessOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,
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

            Response.Cookies.Append("accessToken", tokens.AccessToken, accessOptions);
            Response.Cookies.Append("refreshToken", tokens.RefreshToken, refreshOptions);
        }

        private void ClearTokenCookies()
        {
            var isProd = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(-1)
            };
            Response.Cookies.Delete("accessToken", options);
            Response.Cookies.Delete("refreshToken", options);
        }
    }
}
