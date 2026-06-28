using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JobFinder.Api.Common.Exceptions;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Data;
using JobFinder.Api.Data.Entities;

namespace JobFinder.Api.Services
{
    public interface IRecruiterService
    {
        Task<RecruiterProfileResponseDto> GetProfileAsync(string userId);
        Task<RecruiterProfileResponseDto> UpdateProfileAsync(string userId, RecruiterProfileUpdateDto dto);
        Task<List<RecruiterApplicationResponseDto>> GetApplicationsForJobAsync(string userId, string jobId);
        Task<RecruiterApplicationResponseDto> UpdateApplicationStatusAsync(string userId, string applicationId, ApplicationStatus status);
    }

    public class RecruiterService : IRecruiterService
    {
        private readonly JobFinderDbContext _context;

        public RecruiterService(JobFinderDbContext context)
        {
            _context = context;
        }

        public async Task<RecruiterProfileResponseDto> GetProfileAsync(string userId)
        {
            var profile = await _context.RecruiterProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null) throw new AppException("Recruiter profile not found", 404);

            return MapToProfileDto(profile);
        }

        public async Task<RecruiterProfileResponseDto> UpdateProfileAsync(string userId, RecruiterProfileUpdateDto dto)
        {
            var profile = await _context.RecruiterProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null) throw new AppException("Recruiter profile not found", 404);

            if (dto.CompanyName != null) profile.CompanyName = dto.CompanyName;
            if (dto.CompanyWebsite != null) profile.CompanyWebsite = dto.CompanyWebsite;
            if (dto.Description != null) profile.Description = dto.Description;
            if (dto.Industry != null) profile.Industry = dto.Industry;

            await _context.SaveChangesAsync();

            return MapToProfileDto(profile);
        }

        public async Task<List<RecruiterApplicationResponseDto>> GetApplicationsForJobAsync(string userId, string jobId)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == userId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) throw new AppException("Job not found", 404);

            if (job.RecruiterId != recruiter.Id)
                throw new AppException("You are not authorized to view applications for this job", 403);

            var applications = await _context.Applications
                .Include(a => a.Job)
                .Include(a => a.JobSeeker)
                    .ThenInclude(js => js.User)
                .Where(a => a.JobId == jobId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return applications.Select(MapToRecruiterApplicationDto).ToList();
        }

        public async Task<RecruiterApplicationResponseDto> UpdateApplicationStatusAsync(string userId, string applicationId, ApplicationStatus status)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == userId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var application = await _context.Applications
                .Include(a => a.Job)
                .Include(a => a.JobSeeker)
                    .ThenInclude(js => js.User)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null) throw new AppException("Application not found", 404);

            if (application.Job.RecruiterId != recruiter.Id)
                throw new AppException("You are not authorized to update this application", 403);

            application.Status = status;
            application.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToRecruiterApplicationDto(application);
        }

        private RecruiterProfileResponseDto MapToProfileDto(RecruiterProfile profile)
        {
            return new RecruiterProfileResponseDto
            {
                Id = profile.Id,
                UserId = profile.UserId,
                CompanyName = profile.CompanyName,
                CompanyWebsite = profile.CompanyWebsite,
                Description = profile.Description,
                Industry = profile.Industry,
                User = new UserResponseDto
                {
                    Id = profile.User.Id,
                    Name = profile.User.Name,
                    Email = profile.User.Email,
                    Roles = profile.User.Roles,
                    IsActive = profile.User.IsActive,
                    CreatedAt = profile.User.CreatedAt,
                    UpdatedAt = profile.User.UpdatedAt
                }
            };
        }

        private RecruiterApplicationResponseDto MapToRecruiterApplicationDto(Application a)
        {
            return new RecruiterApplicationResponseDto
            {
                Id = a.Id,
                JobId = a.JobId,
                JobSeekerId = a.JobSeekerId,
                CoverLetter = a.CoverLetter,
                Status = a.Status.ToString(),
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                Job = new JobTitleDto
                {
                    Title = a.Job.Title
                },
                JobSeeker = new JobSeekerShortDto
                {
                    Id = a.JobSeeker.Id,
                    UserId = a.JobSeeker.UserId,
                    Bio = a.JobSeeker.Bio,
                    Location = a.JobSeeker.Location,
                    Skills = a.JobSeeker.Skills,
                    Education = a.JobSeeker.Education,
                    Experience = a.JobSeeker.Experience,
                    ResumeUrl = a.JobSeeker.ResumeUrl,
                    User = new UserShortDto
                    {
                        Name = a.JobSeeker.User.Name,
                        Email = a.JobSeeker.User.Email
                    }
                }
            };
        }
    }
}
