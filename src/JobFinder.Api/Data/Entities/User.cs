// ============================================================
// FILE: Data/Entities/User.cs
// CONCEPT: EF Core Entity class — maps a C# class to a DB table
// ------------------------------------------------------------
// Entity Framework Core uses conventions and data annotations
// ([Key], [Column], [Required]) to understand how to translate
// this class into a PostgreSQL table schema.
//
// The table was originally created by Prisma, so column names
// are camelCase ("id", "email") and the table name is "User"
// (singular) — we override EF's defaults using annotations.
// ============================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Data.Entities
{
    // 📌 CONCEPT: [Table] attribute
    //    By default EF would use the class name "User" for the table.
    //    Here it's already "User", but the annotation makes it explicit
    //    and future-proof (rename the class safely without breaking DB mapping).
    //
    //    EXAMPLE: if the Prisma model were named "AppUser" but the table is "User":
    //      [Table("User")]
    //      public class AppUser { ... }
    [Table("User")]
    public class User
    {
        // 📌 CONCEPT: [Key] — tells EF this property is the PRIMARY KEY.
        //    [Column("id")] maps the C# property "Id" to the Postgres column "id".
        //
        //    Default value = Guid.NewGuid().ToString()
        //    → Every new User gets a random UUID like "a1b2c3d4-..." as its ID.
        //
        //    EXAMPLE:
        //      var user = new User();
        //      Console.WriteLine(user.Id); // "3f7e9a2b-1c4d-..."  (auto-generated)
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // 📌 CONCEPT: [Required] — the column is NOT NULL in the database.
        //    EF will reject SaveChanges if Name is empty/null.
        //
        //    EXAMPLE:
        //      var user = new User { Name = "Alice", Email = "alice@example.com", Password = "hash" };
        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        // 📌 Password stores the BCRYPT HASH, never the plain text.
        //    EXAMPLE hash for "password123" with cost 10:
        //      "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy"
        [Required]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        // 📌 CONCEPT: PostgreSQL enum ARRAY mapped to List<Role>
        //    Npgsql (the PostgreSQL driver) can store a C# list as a Postgres
        //    array column.  The enum type is registered in DbContext.
        //
        //    EXAMPLE database value: {job_seeker,recruiter}
        //    C# value:               new List<Role> { Role.JOB_SEEKER, Role.RECRUITER }
        //
        //    A user starts with one role; they can upgrade to have both
        //    JOB_SEEKER and RECRUITER simultaneously.
        [Column("roles")]
        public List<Role> Roles { get; set; } = new();

        // Active flag — soft disabling an account without deleting data.
        // EXAMPLE: admin bans a user → IsActive = false → they can't log in.
        [Column("isActive")]
        public bool IsActive { get; set; } = true;

        // 📌 CONCEPT: Timestamps
        //    Both default to DateTime.UtcNow (server time, timezone-safe).
        //    UpdatedAt is automatically refreshed by JobFinderDbContext.SaveChangesAsync
        //    (mimics Prisma's @updatedAt behaviour).
        [Column("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ────────────────────────────────────────────────────
        // 📌 CONCEPT: Navigation properties (EF relationships)
        //    These are NOT stored as columns in the User table.
        //    EF uses them to perform JOINs when you call .Include(u => u.JobSeeker).
        //
        //    "virtual" allows EF Core to override the property for lazy loading
        //    (optional feature where related data loads on first access).
        //
        //    "?" means nullable — a user might not have a job-seeker profile yet.
        //
        //    EXAMPLE:
        //      var user = await _context.Users
        //          .Include(u => u.JobSeeker)   // JOIN JobSeekerProfiles
        //          .Include(u => u.Notifications)
        //          .FirstAsync(u => u.Id == id);
        //
        //      Console.WriteLine(user.JobSeeker?.Bio); // "Passionate developer"
        // ────────────────────────────────────────────────────

        // One-to-one: a User may have one JobSeekerProfile.
        public virtual JobSeekerProfile? JobSeeker { get; set; }

        // One-to-one: a User may have one RecruiterProfile.
        public virtual RecruiterProfile? Recruiter { get; set; }

        // One-to-many: a User has many Notifications.
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        // Reports this user FILED against others.
        public virtual ICollection<Report> ReportsMade { get; set; } = new List<Report>();

        // Reports others filed AGAINST this user.
        public virtual ICollection<Report> ReportsReceived { get; set; } = new List<Report>();
    }
}
