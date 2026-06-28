using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Data.Entities
{
    [Table("Notification")]
    public class Notification
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [Column("userId")]
        public string UserId { get; set; } = string.Empty;

        [Column("type")]
        public NotificationType Type { get; set; }

        [Column("isRead")]
        public bool IsRead { get; set; } = false;

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
