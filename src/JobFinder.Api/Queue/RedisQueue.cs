using System;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using JobFinder.Api.Queue.Models;

namespace JobFinder.Api.Queue
{
    public interface IRedisQueue
    {
        Task<QueueJob> EnqueueJobAsync<TPayload>(string type, TPayload payload);
        Task<QueueJob?> GetJobAsync(string jobId);
        Task UpdateJobAsync(QueueJob job);
    }

    public class RedisQueue : IRedisQueue
    {
        private readonly IDatabase _db;
        private const string QueueKey = "db-write-queue";
        private const string JobKeyPrefix = "db-write-job:";
        private static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);

        public RedisQueue(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task<QueueJob> EnqueueJobAsync<TPayload>(string type, TPayload payload)
        {
            var jobId = Guid.NewGuid().ToString();
            var payloadJson = JsonSerializer.Serialize(payload);

            var job = new QueueJob
            {
                Id = jobId,
                Type = type,
                Status = "waiting",
                AttemptsMade = 0,
                CreatedAt = DateTime.UtcNow,
                PayloadJson = payloadJson
            };

            // Save job metadata
            await SaveJobInternalAsync(job);

            // Push to queue list
            await _db.ListRightPushAsync(QueueKey, jobId);

            return job;
        }

        public async Task<QueueJob?> GetJobAsync(string jobId)
        {
            var jobJson = await _db.StringGetAsync($"{JobKeyPrefix}{jobId}");
            if (jobJson.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<QueueJob>(jobJson!);
        }

        public async Task UpdateJobAsync(QueueJob job)
        {
            await SaveJobInternalAsync(job);
        }

        private async Task SaveJobInternalAsync(QueueJob job)
        {
            var json = JsonSerializer.Serialize(job);
            await _db.StringSetAsync($"{JobKeyPrefix}{job.Id}", json, JobTtl);
        }
    }
}
