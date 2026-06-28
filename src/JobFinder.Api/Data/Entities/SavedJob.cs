using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobFinder.Api.Data.Entities
{
    [Table("SavedJob")]
    public class SavedJob
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

        [Column("savedAt")]
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Job Job { get; set; } = null!;
        public virtual JobSeekerProfile JobSeeker { get; set; } = null!;
    }
}
