using System;
using System.Collections.Generic;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Common.Models
{
    // Auth requests
    public class JobSeekerSignupDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RecruiterSignupDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyWebsite { get; set; }
        public string? Description { get; set; }
        public string? Industry { get; set; }
    }

    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RecruiterUpgradeDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyWebsite { get; set; }
        public string? Description { get; set; }
        public string? Industry { get; set; }
    }

    // Token response
    public class TokenResultDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    // Job requests
    public class JobCreateUpdateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public string? Category { get; set; }
    }

    // Profile update requests
    public class JobSeekerProfileUpdateDto
    {
        public string? Bio { get; set; }
        public string? Location { get; set; }
        public List<string>? Skills { get; set; }
        public string? Education { get; set; }
        public string? Experience { get; set; }
        public string? ResumeUrl { get; set; }
    }

    public class RecruiterProfileUpdateDto
    {
        public string? CompanyName { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? Description { get; set; }
        public string? Industry { get; set; }
    }

    // Application requests
    public class ApplicationCreateDto
    {
        public string? CoverLetter { get; set; }
    }

    public class ApplicationStatusDto
    {
        public string Status { get; set; } = string.Empty; // submitted, shortlisted, rejected, under_review
    }

    // Standard responses
    public class UserResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<Role> Roles { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UserMeResponseDto
    {
        public UserResponseDto User { get; set; } = null!;
    }

    public class RecruiterProfileResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyWebsite { get; set; }
        public string? Description { get; set; }
        public string? Industry { get; set; }
        public UserResponseDto User { get; set; } = null!;
    }

    public class JobSeekerProfileResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Location { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? Education { get; set; }
        public string? Experience { get; set; }
        public string? ResumeUrl { get; set; }
        public UserResponseDto User { get; set; } = null!;
    }

    public class JobResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string RecruiterId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public RecruiterCompanyDto? Recruiter { get; set; }
    }

    public class RecruiterCompanyDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string? CompanyWebsite { get; set; }
    }

    public class JobSearchResultDto
    {
        public List<JobResponseDto> Jobs { get; set; } = new();
        public JobsMetaDto Meta { get; set; } = new();
    }

    public class JobsMetaDto
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class ApplicationResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string JobSeekerId { get; set; } = string.Empty;
        public string? CoverLetter { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public JobShortDto Job { get; set; } = null!;
    }

    public class JobShortDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public string? Category { get; set; }
        public RecruiterNameDto Recruiter { get; set; } = null!;
    }

    public class RecruiterNameDto
    {
        public string CompanyName { get; set; } = string.Empty;
    }

    public class RecruiterApplicationResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string JobSeekerId { get; set; } = string.Empty;
        public string? CoverLetter { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public JobTitleDto Job { get; set; } = null!;
        public JobSeekerShortDto JobSeeker { get; set; } = null!;
    }

    public class JobTitleDto
    {
        public string Title { get; set; } = string.Empty;
    }

    public class JobSeekerShortDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Location { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? Education { get; set; }
        public string? Experience { get; set; }
        public string? ResumeUrl { get; set; }
        public UserShortDto User { get; set; } = null!;
    }

    public class UserShortDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class SavedJobResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string JobSeekerId { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
        public JobShortActiveDto Job { get; set; } = null!;
    }

    public class JobShortActiveDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; }
        public RecruiterNameDto Recruiter { get; set; } = null!;
    }
}
