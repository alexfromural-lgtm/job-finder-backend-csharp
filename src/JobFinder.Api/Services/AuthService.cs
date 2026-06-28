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
    public interface IAuthService
    {
        Task<TokenResultDto> SignupJobSeekerAsync(JobSeekerSignupDto dto);
        Task<TokenResultDto> SignupRecruiterAsync(RecruiterSignupDto dto);
        Task<(UserResponseDto User, TokenResultDto Tokens)> UpgradeToRecruiterAsync(string userId, RecruiterUpgradeDto dto);
        Task<TokenResultDto> LoginAsync(LoginDto dto);
        Task<TokenResultDto> RefreshTokensAsync(string refreshToken);
        Task<UserResponseDto> GetCurrentUserAsync(string userId);
    }

    public class AuthService : IAuthService
    {
        private readonly JobFinderDbContext _context;
        private readonly IJwtHelper _jwtHelper;

        public AuthService(JobFinderDbContext context, IJwtHelper jwtHelper)
        {
            _context = context;
            _jwtHelper = jwtHelper;
        }

        public async Task<TokenResultDto> SignupJobSeekerAsync(JobSeekerSignupDto dto)
        {
            ValidateCredentials(dto.Email, dto.Password);
            await CheckUserExistsByEmailAsync(dto.Email);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 10);
            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = passwordHash,
                Roles = new List<Role> { Role.JOB_SEEKER },
                JobSeeker = new JobSeekerProfile()
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return GenerateTokensForUser(user.Id, user.Roles);
        }

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

        public async Task<(UserResponseDto User, TokenResultDto Tokens)> UpgradeToRecruiterAsync(string userId, RecruiterUpgradeDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Recruiter)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) throw new AppException("User not found", 404);
            if (!user.Roles.Contains(Role.JOB_SEEKER))
                throw new AppException("Only Job Seekers can upgrade to Recruiter", 403);
            if (user.Recruiter != null)
                throw new AppException("User already has a recruiter profile", 409);

            user.Roles.Add(Role.RECRUITER);
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

        public async Task<TokenResultDto> LoginAsync(LoginDto dto)
        {
            ValidateCredentials(dto.Email, dto.Password);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) throw new AppException("Invalid email or password", 401);
            if (!user.IsActive) throw new AppException("Account is deactivated", 403);

            var isValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.Password);
            if (!isValid) throw new AppException("Invalid email or password", 401);

            return GenerateTokensForUser(user.Id, user.Roles);
        }

        public async Task<TokenResultDto> RefreshTokensAsync(string refreshToken)
        {
            var principal = _jwtHelper.VerifyRefreshToken(refreshToken);
            if (principal == null)
            {
                throw new AppException("Invalid or expired refresh token", 401);
            }

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

        public async Task<UserResponseDto> GetCurrentUserAsync(string userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserResponseDto
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

        private void ValidateCredentials(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new AppException("Email and password are required", 400);
            }
        }

        private async Task CheckUserExistsByEmailAsync(string email)
        {
            var exists = await _context.Users.AnyAsync(u => u.Email == email);
            if (exists)
            {
                throw new AppException("User already exists", 409);
            }
        }

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
