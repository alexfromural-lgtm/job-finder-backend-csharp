// ============================================================
// FILE: Queue/QueueWorker.cs
// CONCEPT: BackgroundService — long-running hosted service
// ------------------------------------------------------------
// ASP.NET Core's IHostedService / BackgroundService lets you
// run code that keeps running in the background while the
// web server also handles HTTP requests in the same process.
//
// QueueWorker:
//   1. Runs continuously in a loop (while the app is alive).
//   2. Pops job IDs from the Redis queue.
//   3. Loads job metadata and executes the right operation.
//   4. Retries up to 3 times with exponential backoff on failure.
//   5. Marks jobs as "completed" or "failed" in Redis.
// ============================================================

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
    // 📌 CONCEPT: BackgroundService
    //    Inherit from BackgroundService to get:
    //      • Automatic start when the app starts
    //      • Automatic cancellation when the app stops
    //      • ExecuteAsync(CancellationToken) = the method you override with your loop
    //
    //    Registered in Program.cs as:
    //      builder.Services.AddHostedService<QueueWorker>();
    public class QueueWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IRedisQueue _redisQueue;
        private readonly IDatabase _db;
        private readonly ILogger<QueueWorker> _logger;
        private const string QueueKey = "db-write-queue";

        // 📌 CONCEPT: Why IServiceProvider instead of IJobSeekerService directly?
        //    IJobSeekerService is a SCOPED service (new instance per HTTP request).
        //    QueueWorker is a SINGLETON (one instance for the app's lifetime).
        //    You cannot inject a scoped service into a singleton directly —
        //    it would be shared across requests and cause data issues.
        //    Solution: inject IServiceProvider (the DI container itself)
        //    and create a new scope manually for each job: CreateScope().
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

        // ────────────────────────────────────────────────────
        // Main loop — runs until the app shuts down
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: CancellationToken
        //    stoppingToken is cancelled by the framework when the app is shutting down.
        //    We check stoppingToken.IsCancellationRequested in the while condition
        //    to exit cleanly instead of being abruptly killed.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[Worker] db-write-queue worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 📌 CONCEPT: ListLeftPopAsync — non-blocking "try to dequeue"
                    //    Pops from the LEFT of the list (FIFO).
                    //    Returns RedisValue.Null if the queue is empty.
                    var jobIdValue = await _db.ListLeftPopAsync(QueueKey);

                    if (jobIdValue.IsNullOrEmpty)
                    {
                        // No work available — sleep 1 second before checking again.
                        // 📌 stoppingToken passed to Delay so the sleep is interrupted
                        //    immediately if the app is shutting down.
                        await Task.Delay(1000, stoppingToken);
                        continue;  // 📌 "continue" skips the rest of the loop body
                    }

                    string jobId = jobIdValue.ToString();
                    _logger.LogInformation($"[Worker] Popped job #{jobId} from queue.");

                    // 📌 CONCEPT: Fire-and-forget with "_ = Task"
                    //    ProcessJobAsync runs asynchronously WITHOUT waiting for it.
                    //    "_ =" discards the returned Task (we won't await it here).
                    //    This allows the main loop to immediately pop the NEXT job,
                    //    enabling concurrent processing of multiple jobs.
                    //
                    //    TRADE-OFF: unhandled exceptions inside ProcessJobAsync
                    //    won't crash the worker — they're caught inside that method.
                    _ = ProcessJobAsync(jobId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 📌 App is shutting down — exit the loop gracefully.
                    break;
                }
                catch (Exception ex)
                {
                    // Redis connection error or similar — wait longer before retrying.
                    _logger.LogError(ex, "[Worker] Error polling the queue.");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        // ────────────────────────────────────────────────────
        // Process one job
        // ────────────────────────────────────────────────────
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

                // Update status to "active" so we know it's being processed.
                job.Status = "active";
                await _redisQueue.UpdateJobAsync(job);

                // 📌 CONCEPT: Creating a DI scope manually
                //    "using var scope = _serviceProvider.CreateScope()" creates a
                //    temporary scope.  The "using" keyword ensures Dispose() is called
                //    at the end of the block, releasing all scoped resources (DbContext, etc.)
                //
                //    EXAMPLE: each queue job gets its own DbContext connection,
                //    just like each HTTP request does.
                using var scope = _serviceProvider.CreateScope();
                var jobSeekerService = scope.ServiceProvider.GetRequiredService<IJobSeekerService>();

                object? result = null;

                // 📌 CONCEPT: Dispatch by job type string
                //    We check job.Type to call the right service method.
                //    This is a simple "type-based dispatcher" pattern.
                //    A more scalable approach would use a dictionary of handlers.
                //
                //    EXAMPLE job.Type values: "apply-to-job", "save-job"
                if (job.Type == "apply-to-job")
                {
                    // 📌 Deserialize the payload from the JSON stored when job was enqueued.
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

                // ── Success ──────────────────────────────────────
                job.Status = "completed";
                job.AttemptsMade += 1;
                job.ResultJson = JsonSerializer.Serialize(result);  // store result for inspection
                await _redisQueue.UpdateJobAsync(job);

                _logger.LogInformation($"[Worker] ✓ Job #{job.Id} ({job.Type}) succeeded.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Worker] Error processing job #{job.Id}.");

                job.AttemptsMade += 1;

                // 📌 CONCEPT: Retry with backoff
                //    Max 3 attempts.  On each failure:
                //      Attempt 1 fails → wait 500ms then re-queue
                //      Attempt 2 fails → wait 1000ms then re-queue
                //      Attempt 3 fails → mark "failed", stop retrying
                if (job.AttemptsMade >= 3)
                {
                    job.Status = "failed";
                    job.FailedReason = ex.Message;
                    await _redisQueue.UpdateJobAsync(job);
                    _logger.LogError($"[Worker] ✗ Job #{job.Id} ({job.Type}) failed after 3 attempts: {ex.Message}");
                }
                else
                {
                    // Re-queue the job after a short delay.
                    job.Status = "waiting";
                    await _redisQueue.UpdateJobAsync(job);

                    // Exponential backoff: 500ms × attemptsMade
                    var delayMs = 500 * job.AttemptsMade;
                    _logger.LogInformation($"[Worker] Retrying job #{job.Id} in {delayMs}ms (attempt {job.AttemptsMade + 1}/3)...");

                    // 📌 CONCEPT: Task.Run for the delay + re-push
                    //    We don't want to block ProcessJobAsync waiting for the delay.
                    //    Task.Run moves the delay + re-push to a thread pool thread.
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(delayMs, stoppingToken);
                        // Push job ID back to the RIGHT of the list (re-enqueue).
                        await _db.ListRightPushAsync(QueueKey, job.Id);
                    }, stoppingToken);
                }
            }
        }
    }
}
