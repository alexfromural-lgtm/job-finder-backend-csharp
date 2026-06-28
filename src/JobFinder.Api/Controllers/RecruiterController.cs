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
    [Route("api/recruiter")]
    [AuthorizeRoles(Role.RECRUITER)]
    public class RecruiterController : ControllerBase
    {
        private readonly IRecruiterService _recruiterService;
        private readonly IValidator<RecruiterProfileUpdateDto> _profileUpdateValidator;
        private readonly IValidator<ApplicationStatusDto> _statusValidator;

        public RecruiterController(
            IRecruiterService recruiterService,
            IValidator<RecruiterProfileUpdateDto> profileUpdateValidator,
            IValidator<ApplicationStatusDto> statusValidator)
        {
            _recruiterService = recruiterService;
            _profileUpdateValidator = profileUpdateValidator;
            _statusValidator = statusValidator;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst("userId")!.Value;
            var profile = await _recruiterService.GetProfileAsync(userId);
            return Ok(new { profile });
        }

        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] RecruiterProfileUpdateDto dto)
        {
            var validation = await _profileUpdateValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var userId = User.FindFirst("userId")!.Value;
            var profile = await _recruiterService.UpdateProfileAsync(userId, dto);
            return Ok(new { profile });
        }

        [HttpGet("jobs/{jobId}/applications")]
        public async Task<IActionResult> GetApplications(string jobId)
        {
            var userId = User.FindFirst("userId")!.Value;
            var applications = await _recruiterService.GetApplicationsForJobAsync(userId, jobId);
            return Ok(new { applications });
        }

        [HttpPatch("applications/{applicationId}/status")]
        public async Task<IActionResult> UpdateApplicationStatus(string applicationId, [FromBody] ApplicationStatusDto dto)
        {
            var validation = await _statusValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.Errors.First().ErrorMessage });
            }

            var userId = User.FindFirst("userId")!.Value;
            
            // Parse status safely using C# Enum Parse
            if (!System.Enum.TryParse<ApplicationStatus>(dto.Status, out var status))
            {
                return BadRequest(new { error = "Invalid status value" });
            }

            var updatedApplication = await _recruiterService.UpdateApplicationStatusAsync(userId, applicationId, status);
            return Ok(new { application = updatedApplication });
        }
    }
}
