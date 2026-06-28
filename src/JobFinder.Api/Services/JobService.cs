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
    public interface IJobService
    {
        Task<JobResponseDto> CreateJobAsync(string recruiterUserId, JobCreateUpdateDto dto);
        Task<JobResponseDto?> GetJobByIdAsync(string jobId);
        Task<JobResponseDto> UpdateJobAsync(string jobId, string recruiterUserId, JobCreateUpdateDto dto);
        Task DeleteJobAsync(string jobId, string recruiterUserId);
        Task<List<JobResponseDto>> GetJobsByRecruiterAsync(string recruiterUserId);
        Task<JobSearchResultDto> GetAllJobsAsync(string? category, string? location, string? search, int? page, int? pageSize);
    }

    public class JobService : IJobService
    {
        private readonly JobFinderDbContext _context;

        public JobService(JobFinderDbContext context)
        {
            _context = context;
        }

        public async Task<JobResponseDto> CreateJobAsync(string recruiterUserId, JobCreateUpdateDto dto)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == recruiterUserId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var job = new Job
            {
                RecruiterId = recruiter.Id,
                Title = dto.Title,
                Description = dto.Description,
                Requirements = dto.Requirements,
                Location = dto.Location,
                SalaryRange = dto.SalaryRange,
                Category = dto.Category,
                IsActive = true
            };

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            return MapToResponseDto(job, recruiter.CompanyName, recruiter.Industry, recruiter.CompanyWebsite);
        }

        public async Task<JobResponseDto?> GetJobByIdAsync(string jobId)
        {
            var job = await _context.Jobs
                .Include(j => j.Recruiter)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null) return null;

            return MapToResponseDto(job, job.Recruiter.CompanyName, job.Recruiter.Industry, job.Recruiter.CompanyWebsite);
        }

        public async Task<JobResponseDto> UpdateJobAsync(string jobId, string recruiterUserId, JobCreateUpdateDto dto)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == recruiterUserId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) throw new AppException("Job not found", 404);

            if (job.RecruiterId != recruiter.Id)
                throw new AppException("You are not authorized to update this job", 403);

            job.Title = dto.Title;
            job.Description = dto.Description;
            job.Requirements = dto.Requirements;
            job.Location = dto.Location;
            job.SalaryRange = dto.SalaryRange;
            job.Category = dto.Category;
            job.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToResponseDto(job, recruiter.CompanyName, recruiter.Industry, recruiter.CompanyWebsite);
        }

        public async Task DeleteJobAsync(string jobId, string recruiterUserId)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == recruiterUserId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) throw new AppException("Job not found", 404);

            if (job.RecruiterId != recruiter.Id)
                throw new AppException("You are not authorized to delete this job", 403);

            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
        }

        public async Task<List<JobResponseDto>> GetJobsByRecruiterAsync(string recruiterUserId)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == recruiterUserId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var jobs = await _context.Jobs
                .Where(j => j.RecruiterId == recruiter.Id)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return jobs.Select(j => MapToResponseDto(j, recruiter.CompanyName, recruiter.Industry, recruiter.CompanyWebsite)).ToList();
        }

        public async Task<JobSearchResultDto> GetAllJobsAsync(string? category, string? location, string? search, int? page, int? pageSize)
        {
            var currentPage = Math.Max(1, page ?? 1);
            var currentPageSize = Math.Min(100, Math.Max(1, pageSize ?? 10));
            var skip = (currentPage - 1) * currentPageSize;

            var query = _context.Jobs.Include(j => j.Recruiter).Where(j => j.IsActive);

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(j => j.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var lowerLocation = location.ToLower();
                query = query.Where(j => EF.Functions.Like(j.Location.ToLower(), $"%{lowerLocation}%"));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(j => 
                    EF.Functions.Like(j.Title.ToLower(), $"%{lowerSearch}%") || 
                    EF.Functions.Like(j.Description.ToLower(), $"%{lowerSearch}%") || 
                    EF.Functions.Like(j.Requirements.ToLower(), $"%{lowerSearch}%"));
            }

            var total = await query.CountAsync();
            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)
                .Skip(skip)
                .Take(currentPageSize)
                .ToListAsync();

            var jobDtos = jobs.Select(j => MapToResponseDto(j, j.Recruiter.CompanyName, j.Recruiter.Industry, j.Recruiter.CompanyWebsite)).ToList();

            return new JobSearchResultDto
            {
                Jobs = jobDtos,
                Meta = new JobsMetaDto
                {
                    Total = total,
                    Page = currentPage,
                    PageSize = currentPageSize,
                    TotalPages = (int)Math.Ceiling((double)total / currentPageSize)
                }
            };
        }

        private JobResponseDto MapToResponseDto(Job job, string companyName, string? industry, string? companyWebsite)
        {
            return new JobResponseDto
            {
                Id = job.Id,
                RecruiterId = job.RecruiterId,
                Title = job.Title,
                Description = job.Description,
                Requirements = job.Requirements,
                Location = job.Location,
                SalaryRange = job.SalaryRange,
                Category = job.Category,
                IsActive = job.IsActive,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt,
                Recruiter = new RecruiterCompanyDto
                {
                    CompanyName = companyName,
                    Industry = industry,
                    CompanyWebsite = companyWebsite
                }
            };
        }
    }
}
