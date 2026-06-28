using System;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Data.Entities
{
    public class Report
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ReporterId { get; set; } = string.Empty;
        public string? ReportedUserId { get; set; }
        public string? ReportedJobId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ReportStatus Status { get; set; } = ReportStatus.open;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User Reporter { get; set; } = null!;
        public virtual User? ReportedUser { get; set; }
        public virtual Job? ReportedJob { get; set; }
    }
}
