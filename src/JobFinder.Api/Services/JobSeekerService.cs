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
    public interface IJobSeekerService
    {
        Task<JobSeekerProfileResponseDto> GetProfileAsync(string userId);
        Task<JobSeekerProfileResponseDto> UpdateProfileAsync(string userId, JobSeekerProfileUpdateDto dto);
        Task<ApplicationResponseDto> ApplyToJobAsync(string userId, string jobId, string? coverLetter);
        Task<List<ApplicationResponseDto>> GetApplicationsAsync(string userId);
        Task WithdrawApplicationAsync(string userId, string applicationId);
        Task<SavedJobResponseDto> SaveJobAsync(string userId, string jobId);
        Task<List<SavedJobResponseDto>> GetSavedJobsAsync(string userId);
        Task UnsaveJobAsync(string userId, string jobId);
    }

    public class JobSeekerService : IJobSeekerService
    {
        private readonly JobFinderDbContext _context;

        public JobSeekerService(JobFinderDbContext context)
        {
            _context = context;
        }

        public async Task<JobSeekerProfileResponseDto> GetProfileAsync(string userId)
        {
            var profile = await _context.JobSeekerProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null) throw new AppException("Job seeker profile not found", 404);

            return MapToProfileDto(profile);
        }

        public async Task<JobSeekerProfileResponseDto> UpdateProfileAsync(string userId, JobSeekerProfileUpdateDto dto)
        {
            var profile = await _context.JobSeekerProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null) throw new AppException("Job seeker profile not found", 404);

            if (dto.Bio != null) profile.Bio = dto.Bio;
            if (dto.Location != null) profile.Location = dto.Location;
            if (dto.Skills != null) profile.Skills = dto.Skills;
            if (dto.Education != null) profile.Education = dto.Education;
            if (dto.Experience != null) profile.Experience = dto.Experience;
            if (dto.ResumeUrl != null) profile.ResumeUrl = dto.ResumeUrl;

            await _context.SaveChangesAsync();

            return MapToProfileDto(profile);
        }

        public async Task<ApplicationResponseDto> ApplyToJobAsync(string userId, string jobId, string? coverLetter)
        {
            var seeker = await _context.JobSeekerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (seeker == null) throw new AppException("Job seeker profile not found", 404);

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) throw new AppException("Job not found", 404);
            if (!job.IsActive) throw new AppException("This job is no longer active", 410);

            var existing = await _context.Applications.FirstOrDefaultAsync(a => a.JobId == jobId && a.JobSeekerId == seeker.Id);
            if (existing != null) throw new AppException("You have already applied to this job", 409);

            var application = new Application
            {
                JobId = jobId,
                JobSeekerId = seeker.Id,
                CoverLetter = coverLetter,
                Status = ApplicationStatus.submitted
            };

            _context.Applications.Add(application);
            await _context.SaveChangesAsync();

            // Load relations for response mapping
            await _context.Entry(application).Reference(a => a.Job).Query().Include(j => j.Recruiter).LoadAsync();

            return MapToApplicationDto(application);
        }

        public async Task<List<ApplicationResponseDto>> GetApplicationsAsync(string userId)
        {
            var seeker = await _context.JobSeekerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (seeker == null) throw new AppException("Job seeker profile not found", 404);

            var applications = await _context.Applications
                .Include(a => a.Job)
                    .ThenInclude(j => j.Recruiter)
                .Where(a => a.JobSeekerId == seeker.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return applications.Select(MapToApplicationDto).ToList();
        }

        public async Task WithdrawApplicationAsync(string userId, string applicationId)
        {
            var seeker = await _context.JobSeekerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (seeker == null) throw new AppException("Job seeker profile not found", 404);

            var application = await _context.Applications.FirstOrDefaultAsync(a => a.Id == applicationId);
            if (application == null) throw new AppException("Application not found", 404);

            if (application.JobSeekerId != seeker.Id)
                throw new AppException("You are not authorized to withdraw this application", 403);

            _context.Applications.Remove(application);
            await _context.SaveChangesAsync();
        }

        public async Task<SavedJobResponseDto> SaveJobAsync(string userId, string jobId)
        {
            var seeker = await _context.JobSeekerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (seeker == null) throw new AppException("Job seeker profile not found", 404);

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) throw new AppException("Job not found", 404);

            var savedJob = await _context.SavedJobs
                .FirstOrDefaultAsync(sj => sj.JobId == jobId && sj.JobSeekerId == seeker.Id);

            if (savedJob == null)
            {
                savedJob = new SavedJob
                {
                    JobId = jobId,
                    JobSeekerId = seeker.Id,
                    SavedAt = DateTime.UtcNow
                };
                _context.SavedJobs.Add(savedJob);
                await _context.SaveChangesAsync();
            }

            // Load relations for response mapping
            await _context.Entry(savedJob).Reference(sj => sj.Job).Query().Include(j => j.Recruiter).LoadAsync();

            return MapToSavedJobDto(savedJob);
        }

        public async Task<List<SavedJobResponseDto>> GetSavedJobsAsync(string userId)
        {
            var seeker = await _context.JobSeekerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (seeker == null) throw new AppException("Job seeker profile not found", 404);

            var savedJobs = await _context.SavedJobs
                .Include(sj => sj.Job)
                    .ThenInclude(j => j.Recruiter)
                .Where(sj => sj.JobSeekerId == seeker.Id)
                .OrderByDescending(sj => sj.SavedAt)
                .ToListAsync();

            return savedJobs.Select(MapToSavedJobDto).ToList();
        }

        public async Task UnsaveJobAsync(string userId, string jobId)
        {
            var seeker = await _context.JobSeekerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (seeker == null) throw new AppException("Job seeker profile not found", 404);

            var savedJob = await _context.SavedJobs
                .FirstOrDefaultAsync(sj => sj.JobId == jobId && sj.JobSeekerId == seeker.Id);

            if (savedJob == null) throw new AppException("Saved job not found", 404);

            _context.SavedJobs.Remove(savedJob);
            await _context.SaveChangesAsync();
        }

        private JobSeekerProfileResponseDto MapToProfileDto(JobSeekerProfile profile)
        {
            return new JobSeekerProfileResponseDto
            {
                Id = profile.Id,
                UserId = profile.UserId,
                Bio = profile.Bio,
                Location = profile.Location,
                Skills = profile.Skills,
                Education = profile.Education,
                Experience = profile.Experience,
                ResumeUrl = profile.ResumeUrl,
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

        private ApplicationResponseDto MapToApplicationDto(Application a)
        {
            return new ApplicationResponseDto
            {
                Id = a.Id,
                JobId = a.JobId,
                JobSeekerId = a.JobSeekerId,
                CoverLetter = a.CoverLetter,
                Status = a.Status.ToString(),
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                Job = new JobShortDto
                {
                    Id = a.Job.Id,
                    Title = a.Job.Title,
                    Location = a.Job.Location,
                    SalaryRange = a.Job.SalaryRange,
                    Category = a.Job.Category,
                    Recruiter = new RecruiterNameDto
                    {
                        CompanyName = a.Job.Recruiter.CompanyName
                    }
                }
            };
        }

        private SavedJobResponseDto MapToSavedJobDto(SavedJob sj)
        {
            return new SavedJobResponseDto
            {
                Id = sj.Id,
                JobId = sj.JobId,
                JobSeekerId = sj.JobSeekerId,
                SavedAt = sj.SavedAt,
                Job = new JobShortActiveDto
                {
                    Id = sj.Job.Id,
                    Title = sj.Job.Title,
                    Location = sj.Job.Location,
                    SalaryRange = sj.Job.SalaryRange,
                    Category = sj.Job.Category,
                    IsActive = sj.Job.IsActive,
                    Recruiter = new RecruiterNameDto
                    {
                        CompanyName = sj.Job.Recruiter.CompanyName
                    }
                }
            };
        }
    }
}
