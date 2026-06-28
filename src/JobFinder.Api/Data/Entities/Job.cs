using System;
using System.Collections.Generic;

namespace JobFinder.Api.Data.Entities
{
    public class Job
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RecruiterId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual RecruiterProfile Recruiter { get; set; } = null!;
        public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
        public virtual ICollection<SavedJob> SavedBy { get; set; } = new List<SavedJob>();
        public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
    }
}
