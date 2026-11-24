// ============================================================
// FILE: Queue/RedisQueue.cs
// CONCEPT: Redis-backed job queue — async task offloading
// ------------------------------------------------------------
// Some operations (applying to a job, saving a job) are
// offloaded to a background queue so the HTTP response returns
// immediately without waiting for the DB write to complete.
//
// Architecture:
//   HTTP Controller
//     → EnqueueJobAsync() → pushes job ID to Redis LIST
//     → Returns HTTP 202 Accepted immediately
//
//   QueueWorker (background)
//     → Pops job ID from Redis LIST
//     → Loads full job metadata from Redis HASH
//     → Calls the appropriate service method
//     → Updates job status in Redis
//
// Redis data structures used:
//   LIST "db-write-queue"       → ordered job IDs (FIFO queue)
//   STRING "db-write-job:{id}"  → serialized QueueJob metadata (TTL 24h)
// ============================================================

using System;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using JobFinder.Api.Queue.Models;

namespace JobFinder.Api.Queue
{
    // 📌 CONCEPT: Interface for the queue abstraction
    //    QueueWorker and Controllers depend on IRedisQueue.
    //    This makes it easy to swap Redis for another backend
    //    (e.g. RabbitMQ, Azure Service Bus) or mock in tests.
    public interface IRedisQueue
    {
        Task<QueueJob> EnqueueJobAsync<TPayload>(string type, TPayload payload);
        Task<QueueJob?> GetJobAsync(string jobId);
        Task UpdateJobAsync(QueueJob job);
    }

    public class RedisQueue : IRedisQueue
    {
        // 📌 CONCEPT: IDatabase — the Redis connection handle
        //    IConnectionMultiplexer is a long-lived singleton connection.
        //    .GetDatabase() returns a lightweight IDatabase for executing commands.
        private readonly IDatabase _db;

        // Redis key names — constants prevent typos when used in multiple places.
        //
        //    EXAMPLE Redis keys:
        //      db-write-queue              → LIST of job IDs (the queue itself)
        //      db-write-job:abc-123        → STRING with the JSON of one QueueJob
        private const string QueueKey = "db-write-queue";
        private const string JobKeyPrefix = "db-write-job:";

        // 📌 CONCEPT: TTL (Time To Live)
        //    Redis automatically deletes keys after the TTL expires.
        //    24 hours is long enough to inspect failed jobs for debugging,
        //    but not so long we waste Redis memory on old records.
        private static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);

        public RedisQueue(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        // ────────────────────────────────────────────────────
        // ENQUEUE — add a new job to the queue
        // ────────────────────────────────────────────────────

        // 📌 CONCEPT: Generic method <TPayload>
        //    "TPayload" is a type parameter — the caller specifies the concrete type.
        //    EXAMPLE calls:
        //      await EnqueueJobAsync("apply-to-job", new ApplyToJobPayload { ... })
        //      await EnqueueJobAsync("save-job",     new SaveJobPayload { ... })
        //    Both share the same method but payload types are different.
        public async Task<QueueJob> EnqueueJobAsync<TPayload>(string type, TPayload payload)
        {
            // Generate a unique ID for this queue job.
            var jobId = Guid.NewGuid().ToString();

            // 📌 CONCEPT: JsonSerializer.Serialize — convert object to JSON string
            //    TPayload could be any class; Serialize handles it generically.
            //    EXAMPLE:
            //      new ApplyToJobPayload { UserId="u1", JobId="j1" }
            //      → '{"UserId":"u1","JobId":"j1"}'
            var payloadJson = JsonSerializer.Serialize(payload);

            var job = new QueueJob
            {
                Id = jobId,
                Type = type,            // e.g. "apply-to-job"
                Status = "waiting",     // initial status
                AttemptsMade = 0,
                CreatedAt = DateTime.UtcNow,
                PayloadJson = payloadJson
            };

            // Save job metadata to Redis (the full JSON object).
            await SaveJobInternalAsync(job);

            // 📌 CONCEPT: Redis LIST as a FIFO queue
            //    ListRightPushAsync → pushes the jobId to the RIGHT (tail) of the list.
            //    QueueWorker uses ListLeftPopAsync → pops from the LEFT (head).
            //    Right-push + Left-pop = FIFO (first in, first out) queue.
            //
            //    EXAMPLE Redis state after two EnqueueJobAsync calls:
            //      db-write-queue: ["job-1", "job-2"]
            //      worker pops "job-1" first (FIFO)
            await _db.ListRightPushAsync(QueueKey, jobId);

            return job;
        }

        // ────────────────────────────────────────────────────
        // GET JOB — load a job's metadata by ID
        // ────────────────────────────────────────────────────
        public async Task<QueueJob?> GetJobAsync(string jobId)
        {
            // 📌 CONCEPT: Redis STRING — simple key → value store
            //    StringGetAsync fetches the JSON string stored at the key.
            //    Key format: "db-write-job:abc-123"
            var jobJson = await _db.StringGetAsync($"{JobKeyPrefix}{jobId}");

            // 📌 CONCEPT: RedisValue.IsNullOrEmpty
            //    Redis returns "null" for missing keys.
            //    IsNullOrEmpty is the type-safe way to check.
            if (jobJson.IsNullOrEmpty)
            {
                return null;
            }

            // 📌 CONCEPT: JsonSerializer.Deserialize<T>
            //    Convert the JSON string back to a QueueJob object.
            //    "!" asserts the string is not null (we checked above).
            return JsonSerializer.Deserialize<QueueJob>(jobJson!);
        }

        // ────────────────────────────────────────────────────
        // UPDATE JOB — save changed status/result back to Redis
        // ────────────────────────────────────────────────────
        public async Task UpdateJobAsync(QueueJob job)
        {
            await SaveJobInternalAsync(job);
        }

        // ────────────────────────────────────────────────────
        // Private helper — serialize & store job in Redis
        // ────────────────────────────────────────────────────
        private async Task SaveJobInternalAsync(QueueJob job)
        {
            var json = JsonSerializer.Serialize(job);

            // 📌 CONCEPT: StringSetAsync with TTL
            //    Stores the JSON string at "db-write-job:{id}".
            //    JobTtl = 24 hours — Redis auto-deletes after that.
            await _db.StringSetAsync($"{JobKeyPrefix}{job.Id}", json, JobTtl);
        }
    }
}
