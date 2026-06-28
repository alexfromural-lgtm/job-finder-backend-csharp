using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Data.Entities
{
    [Table("Report")]
    public class Report
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [Column("reporterId")]
        public string ReporterId { get; set; } = string.Empty;

        [Column("reportedUserId")]
        public string? ReportedUserId { get; set; }

        [Column("reportedJobId")]
        public string? ReportedJobId { get; set; }

        [Required]
        [Column("reason")]
        public string Reason { get; set; } = string.Empty;

        [Column("status")]
        public ReportStatus Status { get; set; } = ReportStatus.open;

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User Reporter { get; set; } = null!;
        public virtual User? ReportedUser { get; set; }
        public virtual Job? ReportedJob { get; set; }
    }
}
