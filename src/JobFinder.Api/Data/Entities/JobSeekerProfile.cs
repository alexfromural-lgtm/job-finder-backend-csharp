using System;
using System.Collections.Generic;

namespace JobFinder.Api.Data.Entities
{
    public class JobSeekerProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Location { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? Education { get; set; }
        public string? Experience { get; set; }
        public string? ResumeUrl { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
        public virtual ICollection<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();
    }
}
