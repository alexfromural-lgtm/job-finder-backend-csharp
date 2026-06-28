using System;
using System.Collections.Generic;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Data.Entities
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        
        // Postgres enum array support: Role[]
        public List<Role> Roles { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual JobSeekerProfile? JobSeeker { get; set; }
        public virtual RecruiterProfile? Recruiter { get; set; }
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<Report> ReportsMade { get; set; } = new List<Report>();
        public virtual ICollection<Report> ReportsReceived { get; set; } = new List<Report>();
    }
}
