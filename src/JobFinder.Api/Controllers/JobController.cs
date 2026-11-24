// ============================================================
// FILE: Controllers/JobController.cs
// CONCEPT: RESTful resource controller
// ------------------------------------------------------------
// REST maps HTTP verbs to CRUD operations:
//   POST   → Create
//   GET    → Read
//   PUT    → Update (full replacement)
//   DELETE → Delete
//
// Route: /api/jobs
//   POST   /api/jobs              → create job         (recruiter)
//   GET    /api/jobs/{jobId}      → get one job        (public)
//   PUT    /api/jobs/{jobId}      → update job         (recruiter)
//   DELETE /api/jobs/{jobId}      → delete job         (recruiter)
//   GET    /api/jobs/recruiter    → recruiter's jobs    (recruiter)
//   GET    /api/jobs/all          → paginated listing   (public)
// ============================================================

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Middleware;
using JobFinder.Api.Services;

namespace JobFinder.Api.Controllers
{
    [ApiController]
    [Route("api/jobs")]
    public class JobController : ControllerBase
    {
        private readonly IJobService _jobService;
        private readonly IValidator<JobCreateUpdateDto> _jobValidator;

        public JobController(IJobService jobService, IValidator<JobCreateUpdateDto> jobValidator)
        {
            _jobService = jobService;
            _jobValidator = jobValidator;
        }

        // ────────────────────────────────────────────────────
        // POST /api/jobs — Create a new job listing
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: [HttpPost] without a path means the route IS the base route
        //    Combined with [Route("api/jobs")]: POST /api/jobs
        //
        //    [AuthorizeRoles(Role.RECRUITER)] blocks non-recruiters before
        //    the action body even executes.
        [HttpPost]
        [AuthorizeRoles(Role.RECRUITER)]
        public async Task<IActionResult> CreateJob([FromBody] JobCreateUpdateDto dto)
        {
            // Validate input before touching the database.
            var validation = await _jobValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            // Extract the recruiter's userId from the JWT (populated by AuthMiddleware).
            var userId = User.FindFirst("userId")!.Value;

            var job = await _jobService.CreateJobAsync(userId, dto);

            // 📌 StatusCode(201) = "Created" — resource was successfully created.
            //    Convention: respond with the created resource so the client
            //    has the generated ID immediately.
            return StatusCode(201, new { job });
        }

        // ────────────────────────────────────────────────────
        // GET /api/jobs/{jobId} — Get a single job (public)
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Route parameter "{jobId}"
        //    The {jobId} segment in the route template is bound to the
        //    "jobId" method parameter automatically by ASP.NET Core.
        //
        //    EXAMPLE:
        //      GET /api/jobs/abc-123
        //      → jobId = "abc-123"
        [HttpGet("{jobId}")]
        public async Task<IActionResult> GetJobById(string jobId)
        {
            var job = await _jobService.GetJobByIdAsync(jobId);

            // 📌 CONCEPT: null check → HTTP 404
            //    Service returns null when no job has that ID.
            //    Controller translates null → NotFound() (HTTP 404).
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            return Ok(new { job });
        }

        // ────────────────────────────────────────────────────
        // PUT /api/jobs/{jobId} — Full update (recruiter only)
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: PUT vs PATCH
        //    PUT = full replacement of the resource (all fields sent every time).
        //    PATCH = partial update (only changed fields sent).
        //    This API uses PUT for simplicity — the form always sends all fields.
        [HttpPut("{jobId}")]
        [AuthorizeRoles(Role.RECRUITER)]
        public async Task<IActionResult> UpdateJob(string jobId, [FromBody] JobCreateUpdateDto dto)
        {
            var validation = await _jobValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var userId = User.FindFirst("userId")!.Value;
            var job = await _jobService.UpdateJobAsync(jobId, userId, dto);
            return Ok(new { job });
        }

        // ────────────────────────────────────────────────────
        // DELETE /api/jobs/{jobId}
        // ────────────────────────────────────────────────────
        [HttpDelete("{jobId}")]
        [AuthorizeRoles(Role.RECRUITER)]
        public async Task<IActionResult> DeleteJob(string jobId)
        {
            var userId = User.FindFirst("userId")!.Value;
            await _jobService.DeleteJobAsync(jobId, userId);

            // 📌 CONCEPT: Return a message even for DELETE
            //    Some APIs return 204 No Content.  Here we return 200 + message
            //    to match the Node.js API's behaviour.
            return Ok(new { message = "Job deleted successfully" });
        }

        // ────────────────────────────────────────────────────
        // GET /api/jobs/recruiter — Recruiter's own job listings
        // ────────────────────────────────────────────────────

        // 📌 IMPORTANT: "/recruiter" must be declared BEFORE "/{jobId}"
        //    ASP.NET Core matches routes in registration order.
        //    If /{jobId} came first, "recruiter" would be treated as a jobId value.
        [HttpGet("recruiter")]
        [AuthorizeRoles(Role.RECRUITER)]
        public async Task<IActionResult> GetMyJobs()
        {
            var userId = User.FindFirst("userId")!.Value;
            var jobs = await _jobService.GetJobsByRecruiterAsync(userId);
            return Ok(new { jobs });
        }

        // ────────────────────────────────────────────────────
        // GET /api/jobs/all — Paginated public job listing
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: [FromQuery] — bind URL query parameters to method params
        //    EXAMPLE URL:
        //      GET /api/jobs/all?category=Engineering&location=remote&page=2&pageSize=5
        //      → category = "Engineering"
        //      → location = "remote"
        //      → page = 2
        //      → pageSize = 5
        //
        //    "?" makes params optional — they can be omitted from the URL.
        //    "int?" means the value is an optional integer (null if not provided).
        [HttpGet("all")]
        public async Task<IActionResult> GetAllJobs(
            [FromQuery] string? category,
            [FromQuery] string? location,
            [FromQuery] string? search,
            [FromQuery] int? page,
            [FromQuery] int? pageSize)
        {
            var result = await _jobService.GetAllJobsAsync(category, location, search, page, pageSize);

            // 📌 CONCEPT: Return the whole DTO directly (no wrapper property)
            //    result is already a JobSearchResultDto with { jobs: [...], meta: {...} }
            //    "return Ok(result)" serializes it as-is.
            return Ok(result);
        }
    }
}
