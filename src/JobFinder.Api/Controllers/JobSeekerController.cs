using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Middleware;
using JobFinder.Api.Queue;
using JobFinder.Api.Queue.Models;
using JobFinder.Api.Services;

namespace JobFinder.Api.Controllers
{
    [ApiController]
    [Route("api/jobseeker")]
    [AuthorizeRoles(Role.JOB_SEEKER)]
    public class JobSeekerController : ControllerBase
    {
        private readonly IJobSeekerService _jobSeekerService;
        private readonly IRedisQueue _redisQueue;
        private readonly IValidator<JobSeekerProfileUpdateDto> _profileUpdateValidator;
        private readonly IValidator<ApplicationCreateDto> _applicationCreateValidator;

        public JobSeekerController(
            IJobSeekerService jobSeekerService,
            IRedisQueue redisQueue,
            IValidator<JobSeekerProfileUpdateDto> profileUpdateValidator,
            IValidator<ApplicationCreateDto> applicationCreateValidator)
        {
            _jobSeekerService = jobSeekerService;
            _redisQueue = redisQueue;
            _profileUpdateValidator = profileUpdateValidator;
            _applicationCreateValidator = applicationCreateValidator;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst("userId")!.Value;
            var profile = await _jobSeekerService.GetProfileAsync(userId);
            return Ok(new { profile });
        }

        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] JobSeekerProfileUpdateDto dto)
        {
            var validation = await _profileUpdateValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var userId = User.FindFirst("userId")!.Value;
            var profile = await _jobSeekerService.UpdateProfileAsync(userId, dto);
            return Ok(new { profile });
        }

        [HttpPost("apply/{jobId}")]
        public async Task<IActionResult> ApplyToJob(string jobId, [FromBody] ApplicationCreateDto dto)
        {
            var validation = await _applicationCreateValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var userId = User.FindFirst("userId")!.Value;

            // Enqueue the DB write operation to Redis queue instead of direct writing
            var queueJob = await _redisQueue.EnqueueJobAsync("apply-to-job", new ApplyToJobPayload
            {
                UserId = userId,
                JobId = jobId,
                CoverLetter = dto.CoverLetter
            });

            return Accepted(new
            {
                queueJobId = queueJob.Id,
                status = "queued"
            });
        }

        [HttpPost("saved/{jobId}")]
        public async Task<IActionResult> SaveJob(string jobId)
        {
            var userId = User.FindFirst("userId")!.Value;

            // Enqueue the DB write operation to Redis queue
            var queueJob = await _redisQueue.EnqueueJobAsync("save-job", new SaveJobPayload
            {
                UserId = userId,
                JobId = jobId
            });

            return Accepted(new
            {
                queueJobId = queueJob.Id,
                status = "queued"
            });
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications()
        {
            var userId = User.FindFirst("userId")!.Value;
            var applications = await _jobSeekerService.GetApplicationsAsync(userId);
            return Ok(new { applications });
        }

        [HttpDelete("applications/{applicationId}")]
        public async Task<IActionResult> WithdrawApplication(string applicationId)
        {
            var userId = User.FindFirst("userId")!.Value;
            await _jobSeekerService.WithdrawApplicationAsync(userId, applicationId);
            return Ok(new { message = "Application withdrawn successfully" });
        }

        [HttpGet("saved")]
        public async Task<IActionResult> GetSavedJobs()
        {
            var userId = User.FindFirst("userId")!.Value;
            var savedJobs = await _jobSeekerService.GetSavedJobsAsync(userId);
            return Ok(new { savedJobs });
        }

        [HttpDelete("saved/{jobId}")]
        public async Task<IActionResult> UnsaveJob(string jobId)
        {
            var userId = User.FindFirst("userId")!.Value;
            await _jobSeekerService.UnsaveJobAsync(userId, jobId);
            return Ok(new { message = "Job unsaved successfully" });
        }
    }
}
