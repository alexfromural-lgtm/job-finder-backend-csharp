// ============================================================
// FILE: Services/JobService.cs
// CONCEPT: CRUD service with filtering, pagination and ownership checks
// ------------------------------------------------------------
// JobService handles all job-related business logic:
//   • Create / Read / Update / Delete (CRUD) for jobs
//   • Authorisation: only the job's owner recruiter may edit/delete
//   • Search with optional filters: category, location, free text
//   • Pagination: return a page of results + metadata
// ============================================================

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
    // 📌 CONCEPT: Interface declaring the contract
    //    Every method returns Task<T> (async).
    //    "?" after a type means the method may return null
    //    (e.g. GetJobByIdAsync returns null when the job doesn't exist).
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

        // ────────────────────────────────────────────────────
        // CREATE JOB
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Ownership via recruiterUserId
        //    The JWT contains the userId, NOT the recruiterId.
        //    We first look up the RecruiterProfile by UserId to get
        //    the recruiter's profile ID (the foreign key on Job).
        //
        //    EXAMPLE:
        //      userId     = "u-abc"   (from JWT claim)
        //      recruiterId = "rp-xyz" (looked up from RecruiterProfiles)
        //      new Job { RecruiterId = "rp-xyz", ... }
        public async Task<JobResponseDto> CreateJobAsync(string recruiterUserId, JobCreateUpdateDto dto)
        {
            // Find the recruiter's profile row (not the User row).
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == recruiterUserId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var job = new Job
            {
                RecruiterId = recruiter.Id,     // FK to RecruiterProfile.Id
                Title = dto.Title,
                Description = dto.Description,
                Requirements = dto.Requirements,
                Location = dto.Location,
                SalaryRange = dto.SalaryRange,
                Category = dto.Category,
                IsActive = true                 // new jobs are active by default
            };

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            // 📌 CONCEPT: MapToResponseDto — always return a DTO, never the raw entity
            //    The entity has navigation properties and EF tracking overhead.
            //    The DTO is a plain object safe to serialise to JSON.
            return MapToResponseDto(job, recruiter.CompanyName, recruiter.Industry, recruiter.CompanyWebsite);
        }

        // ────────────────────────────────────────────────────
        // GET SINGLE JOB (public — no auth required)
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: .Include() for eager loading
        //    Without Include, job.Recruiter would be null.
        //    With Include, EF performs a JOIN in one SQL query.
        //
        //    SQL: SELECT j.*, rp.*
        //         FROM "Job" j
        //         INNER JOIN "RecruiterProfile" rp ON j."recruiterId" = rp.id
        //         WHERE j.id = @jobId
        public async Task<JobResponseDto?> GetJobByIdAsync(string jobId)
        {
            var job = await _context.Jobs
                .Include(j => j.Recruiter)              // load related RecruiterProfile
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null) return null;  // controller returns HTTP 404

            return MapToResponseDto(job, job.Recruiter.CompanyName, job.Recruiter.Industry, job.Recruiter.CompanyWebsite);
        }

        // ────────────────────────────────────────────────────
        // UPDATE JOB
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Ownership check before mutation
        //    We verify the recruiter who calls this endpoint
        //    actually OWNS the job before updating it.
        //    Without this check, any recruiter could edit any job.
        public async Task<JobResponseDto> UpdateJobAsync(string jobId, string recruiterUserId, JobCreateUpdateDto dto)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == recruiterUserId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) throw new AppException("Job not found", 404);

            // 📌 CONCEPT: Authorisation at the data level (not just at the route level)
            //    [AuthorizeRoles(Role.RECRUITER)] ensures only recruiters reach this method.
            //    But this check ensures they can only modify THEIR OWN jobs.
            if (job.RecruiterId != recruiter.Id)
                throw new AppException("You are not authorized to update this job", 403);

            // 📌 CONCEPT: Updating a tracked entity
            //    EF's ChangeTracker detects that "job" is Modified.
            //    SaveChangesAsync generates UPDATE SQL only for changed columns.
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

        // ────────────────────────────────────────────────────
        // DELETE JOB
        // ────────────────────────────────────────────────────
        public async Task DeleteJobAsync(string jobId, string recruiterUserId)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == recruiterUserId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) throw new AppException("Job not found", 404);

            if (job.RecruiterId != recruiter.Id)
                throw new AppException("You are not authorized to delete this job", 403);

            // 📌 CONCEPT: .Remove() marks the entity for deletion.
            //    SaveChangesAsync generates: DELETE FROM "Job" WHERE id = @id
            //    Because of OnDelete(Cascade) in DbContext, related Applications
            //    and SavedJobs are also deleted automatically.
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
        }

        // ────────────────────────────────────────────────────
        // GET RECRUITER'S OWN JOBS
        // ────────────────────────────────────────────────────
        public async Task<List<JobResponseDto>> GetJobsByRecruiterAsync(string recruiterUserId)
        {
            var recruiter = await _context.RecruiterProfiles.FirstOrDefaultAsync(rp => rp.UserId == recruiterUserId);
            if (recruiter == null) throw new AppException("Recruiter profile not found", 404);

            var jobs = await _context.Jobs
                .Where(j => j.RecruiterId == recruiter.Id)
                .OrderByDescending(j => j.CreatedAt)  // newest first
                .ToListAsync();

            // 📌 CONCEPT: .Select() with LINQ to transform the list
            //    jobs.Select(j => MapToResponseDto(...)) converts each Job entity
            //    to a JobResponseDto.  .ToList() materialises the lazy IEnumerable.
            return jobs.Select(j => MapToResponseDto(j, recruiter.CompanyName, recruiter.Industry, recruiter.CompanyWebsite)).ToList();
        }

        // ────────────────────────────────────────────────────
        // GET ALL JOBS (public, with filters and pagination)
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Dynamic LINQ query building
        //    We start with a base query and conditionally ADD WHERE clauses.
        //    EF Core builds ONE SQL query at the end — no N+1 problem.
        //
        //    EXAMPLE URLs:
        //      GET /api/jobs/all                              → all active jobs, page 1
        //      GET /api/jobs/all?category=Engineering         → filter by category
        //      GET /api/jobs/all?search=dotnet&page=2         → search + page 2
        //      GET /api/jobs/all?location=remote&pageSize=5   → location filter, 5 per page
        public async Task<JobSearchResultDto> GetAllJobsAsync(string? category, string? location, string? search, int? page, int? pageSize)
        {
            // 📌 CONCEPT: Clamp / default values
            //    Math.Max(1, page ?? 1)      → at least page 1 (never page 0 or negative)
            //    Math.Min(100, pageSize ?? 10) → at most 100 results per page
            var currentPage = Math.Max(1, page ?? 1);
            var currentPageSize = Math.Min(100, Math.Max(1, pageSize ?? 10));
            var skip = (currentPage - 1) * currentPageSize;  // offset for SQL SKIP

            // Build base query: only active jobs, with recruiter info eager-loaded.
            var query = _context.Jobs.Include(j => j.Recruiter).Where(j => j.IsActive);

            // Conditionally add filter clauses:
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(j => j.Category == category);  // exact match
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                // 📌 CONCEPT: Case-insensitive LIKE search
                //    EF.Functions.Like(...) translates to PostgreSQL's ILIKE (case-insensitive).
                //    "%" is a wildcard matching any sequence of characters.
                //    EXAMPLE: location="remote" matches "Remote", "remote work", "REMOTE"
                var lowerLocation = location.ToLower();
                query = query.Where(j => EF.Functions.Like(j.Location.ToLower(), $"%{lowerLocation}%"));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                // 📌 Free-text search across title, description, requirements.
                //    Uses OR logic so a match in any field is returned.
                var lowerSearch = search.ToLower();
                query = query.Where(j =>
                    EF.Functions.Like(j.Title.ToLower(), $"%{lowerSearch}%") ||
                    EF.Functions.Like(j.Description.ToLower(), $"%{lowerSearch}%") ||
                    EF.Functions.Like(j.Requirements.ToLower(), $"%{lowerSearch}%"));
            }

            // 📌 CONCEPT: Two queries — count THEN page
            //    CountAsync runs: SELECT COUNT(*) FROM ...
            //    The second query adds ORDER BY, OFFSET, LIMIT.
            var total = await query.CountAsync();
            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)  // newest first
                .Skip(skip)                            // skip previous pages
                .Take(currentPageSize)                 // take this page's worth
                .ToListAsync();

            var jobDtos = jobs.Select(j =>
                MapToResponseDto(j, j.Recruiter.CompanyName, j.Recruiter.Industry, j.Recruiter.CompanyWebsite)
            ).ToList();

            return new JobSearchResultDto
            {
                Jobs = jobDtos,
                Meta = new JobsMetaDto
                {
                    Total = total,
                    Page = currentPage,
                    PageSize = currentPageSize,
                    // 📌 CONCEPT: Math.Ceiling for page count
                    //    If total=25 and pageSize=10 → 2.5 → ceil → 3 pages.
                    TotalPages = (int)Math.Ceiling((double)total / currentPageSize)
                }
            };
        }

        // ────────────────────────────────────────────────────
        // Private helper: entity → DTO
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Private mapping method (poor-man's AutoMapper)
        //    Re-used by all CRUD methods to keep the mapping logic in one place.
        //    If the DTO shape changes, we only update this one method.
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
