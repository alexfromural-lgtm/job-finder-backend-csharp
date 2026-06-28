using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using JobFinder.Api.Queue;
using JobFinder.Api.Queue.Models;

namespace JobFinder.Api.Controllers
{
    [ApiController]
    [Route("api/queue")]
    public class QueueController : ControllerBase
    {
        private readonly IRedisQueue _redisQueue;

        public QueueController(IRedisQueue redisQueue)
        {
            _redisQueue = redisQueue;
        }

        /// <summary>
        /// GET /api/queue/job/:jobId
        /// Allows the client to poll the status of a queued write operation.
        /// Response shape matches the frontend's QueueJobResponse type exactly.
        /// </summary>
        [HttpGet("job/{jobId}")]
        public async Task<IActionResult> GetJobStatus(string jobId)
        {
            var job = await _redisQueue.GetJobAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            // Deserialize result payload if present
            object? result = null;
            if (!string.IsNullOrEmpty(job.ResultJson))
            {
                result = JsonSerializer.Deserialize<object>(job.ResultJson);
            }

            // Response shape matches frontend QueueJobResponse interface:
            // { id, type, status, attemptsMade, createdAt, result?, failedReason? }
            return Ok(new
            {
                id = job.Id,
                type = job.Type,
                status = job.Status,       // "waiting" | "active" | "completed" | "failed"
                attemptsMade = job.AttemptsMade,
                createdAt = job.CreatedAt.ToString("o"), // ISO 8601
                result,
                failedReason = job.FailedReason
            });
        }
    }
}
