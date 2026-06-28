using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobFinder.Api.Data.Entities
{
    [Table("JobSeekerProfile")]
    public class JobSeekerProfile
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [Column("userId")]
        public string UserId { get; set; } = string.Empty;

        [Column("bio")]
        public string? Bio { get; set; }

        [Column("location")]
        public string? Location { get; set; }

        /// <summary>
        /// Stored as a PostgreSQL text[] array.
        /// </summary>
        [Column("skills")]
        public List<string> Skills { get; set; } = new();

        [Column("education")]
        public string? Education { get; set; }

        [Column("experience")]
        public string? Experience { get; set; }

        [Column("resumeUrl")]
        public string? ResumeUrl { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
        public virtual ICollection<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();
    }
}
