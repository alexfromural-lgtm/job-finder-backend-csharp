using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Data.Entities
{
    [Table("Application")]
    public class Application
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [Column("jobId")]
        public string JobId { get; set; } = string.Empty;

        [Required]
        [Column("jobSeekerId")]
        public string JobSeekerId { get; set; } = string.Empty;

        [Column("coverLetter")]
        public string? CoverLetter { get; set; }

        [Column("status")]
        public ApplicationStatus Status { get; set; } = ApplicationStatus.submitted;

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Job Job { get; set; } = null!;
        public virtual JobSeekerProfile JobSeeker { get; set; } = null!;
    }
}
