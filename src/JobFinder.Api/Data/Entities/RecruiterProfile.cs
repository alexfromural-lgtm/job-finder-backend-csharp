using System;
using System.Collections.Generic;

namespace JobFinder.Api.Data.Entities
{
    public class RecruiterProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyWebsite { get; set; }
        public string? Description { get; set; }
        public string? Industry { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
    }
}
