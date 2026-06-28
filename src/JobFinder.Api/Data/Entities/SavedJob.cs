using System;

namespace JobFinder.Api.Data.Entities
{
    public class SavedJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string JobId { get; set; } = string.Empty;
        public string JobSeekerId { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Job Job { get; set; } = null!;
        public virtual JobSeekerProfile JobSeeker { get; set; } = null!;
    }
}
