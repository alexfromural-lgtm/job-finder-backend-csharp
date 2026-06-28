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

        [HttpPost]
        [AuthorizeRoles(Role.RECRUITER)]
        public async Task<IActionResult> CreateJob([FromBody] JobCreateUpdateDto dto)
        {
            var validation = await _jobValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var userId = User.FindFirst("userId")!.Value;
            var job = await _jobService.CreateJobAsync(userId, dto);
            return StatusCode(201, new { job });
        }

        [HttpGet("{jobId}")]
        public async Task<IActionResult> GetJobById(string jobId)
        {
            var job = await _jobService.GetJobByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            return Ok(new { job });
        }

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

        [HttpDelete("{jobId}")]
        [AuthorizeRoles(Role.RECRUITER)]
        public async Task<IActionResult> DeleteJob(string jobId)
        {
            var userId = User.FindFirst("userId")!.Value;
            await _jobService.DeleteJobAsync(jobId, userId);
            return Ok(new { message = "Job deleted successfully" });
        }

        [HttpGet("recruiter")]
        [AuthorizeRoles(Role.RECRUITER)]
        public async Task<IActionResult> GetMyJobs()
        {
            var userId = User.FindFirst("userId")!.Value;
            var jobs = await _jobService.GetJobsByRecruiterAsync(userId);
            return Ok(new { jobs });
        }

        // GET /api/jobs/all  — public job listing with filters (matches Node.js route exactly)
        [HttpGet("all")]
        public async Task<IActionResult> GetAllJobs(
            [FromQuery] string? category,
            [FromQuery] string? location,
            [FromQuery] string? search,
            [FromQuery] int? page,
            [FromQuery] int? pageSize)
        {
            var result = await _jobService.GetAllJobsAsync(category, location, search, page, pageSize);
            return Ok(result);
        }
    }
}
