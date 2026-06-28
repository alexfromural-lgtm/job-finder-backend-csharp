using System;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Data.Entities
{
    public class Application
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string JobId { get; set; } = string.Empty;
        public string JobSeekerId { get; set; } = string.Empty;
        public string? CoverLetter { get; set; }
        public ApplicationStatus Status { get; set; } = ApplicationStatus.submitted;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Job Job { get; set; } = null!;
        public virtual JobSeekerProfile JobSeeker { get; set; } = null!;
    }
}
