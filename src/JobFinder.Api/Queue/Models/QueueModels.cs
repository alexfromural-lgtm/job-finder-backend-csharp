using System;

namespace JobFinder.Api.Queue.Models
{
    public class QueueJob
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "apply-to-job" or "save-job"
        public string Status { get; set; } = "waiting"; // waiting, active, completed, failed
        public int AttemptsMade { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string PayloadJson { get; set; } = string.Empty;
        public string? ResultJson { get; set; }
        public string? FailedReason { get; set; }
    }

    public class ApplyToJobPayload
    {
        public string UserId { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string? CoverLetter { get; set; }
    }

    public class SaveJobPayload
    {
        public string UserId { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
    }
}
