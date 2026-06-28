using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using JobFinder.Api.Queue.Models;
using JobFinder.Api.Services;

namespace JobFinder.Api.Queue
{
    public class QueueWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IRedisQueue _redisQueue;
        private readonly IDatabase _db;
        private readonly ILogger<QueueWorker> _logger;
        private const string QueueKey = "db-write-queue";

        public QueueWorker(
            IServiceProvider serviceProvider,
            IRedisQueue redisQueue,
            IConnectionMultiplexer redis,
            ILogger<QueueWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _redisQueue = redisQueue;
            _db = redis.GetDatabase();
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[Worker] db-write-queue worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Pop job ID from the left of the list
                    var jobIdValue = await _db.ListLeftPopAsync(QueueKey);
                    if (jobIdValue.IsNullOrEmpty)
                    {
                        // No job in queue, wait a bit before checking again
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    string jobId = jobIdValue.ToString();
                    _logger.LogInformation($"[Worker] Popped job #{jobId} from queue.");

                    // Run the job processing in a separate task so we can process concurrently if concurrency > 1,
                    // or just await it if we want sequential execution.
                    // Fire-and-forget-ish task but tracking it for cancellation is best.
                    _ = ProcessJobAsync(jobId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Worker] Error polling the queue.");
                    await Task.Delay(5000, stoppingToken); // Wait longer on error
                }
            }
        }

        private async Task ProcessJobAsync(string jobId, CancellationToken stoppingToken)
        {
            var job = await _redisQueue.GetJobAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"[Worker] Job #{jobId} details not found in Redis.");
                return;
            }

            try
            {
                _logger.LogInformation($"[Worker] Processing job #{job.Id} | type: {job.Type}");
                job.Status = "active";
                await _redisQueue.UpdateJobAsync(job);

                using var scope = _serviceProvider.CreateScope();
                var jobSeekerService = scope.ServiceProvider.GetRequiredService<IJobSeekerService>();

                object? result = null;

                if (job.Type == "apply-to-job")
                {
                    var payload = JsonSerializer.Deserialize<ApplyToJobPayload>(job.PayloadJson);
                    if (payload == null) throw new InvalidOperationException("Invalid payload for apply-to-job.");

                    result = await jobSeekerService.ApplyToJobAsync(payload.UserId, payload.JobId, payload.CoverLetter);
                }
                else if (job.Type == "save-job")
                {
                    var payload = JsonSerializer.Deserialize<SaveJobPayload>(job.PayloadJson);
                    if (payload == null) throw new InvalidOperationException("Invalid payload for save-job.");

                    result = await jobSeekerService.SaveJobAsync(payload.UserId, payload.JobId);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown job type: {job.Type}");
                }

                // Success
                job.Status = "completed";
                job.AttemptsMade += 1;
                job.ResultJson = JsonSerializer.Serialize(result);
                await _redisQueue.UpdateJobAsync(job);

                _logger.LogInformation($"[Worker] ✓ Job #{job.Id} ({job.Type}) succeeded.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Worker] Error processing job #{job.Id}.");

                job.AttemptsMade += 1;
                
                if (job.AttemptsMade >= 3)
                {
                    // Max attempts reached, fail the job
                    job.Status = "failed";
                    job.FailedReason = ex.Message;
                    await _redisQueue.UpdateJobAsync(job);
                    _logger.LogError($"[Worker] ✗ Job #{job.Id} ({job.Type}) failed after 3 attempts: {ex.Message}");
                }
                else
                {
                    // Re-enqueue after a backoff delay
                    job.Status = "waiting";
                    await _redisQueue.UpdateJobAsync(job);

                    var delayMs = 500 * job.AttemptsMade;
                    _logger.LogInformation($"[Worker] Retrying job #{job.Id} in {delayMs}ms (attempt {job.AttemptsMade + 1}/3)...");
                    
                    // Run the delay and push back to queue in background
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(delayMs, stoppingToken);
                        await _db.ListRightPushAsync(QueueKey, job.Id);
                    }, stoppingToken);
                }
            }
        }
    }
}
