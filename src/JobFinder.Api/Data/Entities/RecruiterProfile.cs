using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobFinder.Api.Data.Entities
{
    [Table("RecruiterProfile")]
    public class RecruiterProfile
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [Column("userId")]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Column("companyName")]
        public string CompanyName { get; set; } = string.Empty;

        [Column("companyWebsite")]
        public string? CompanyWebsite { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("industry")]
        public string? Industry { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
    }
}
