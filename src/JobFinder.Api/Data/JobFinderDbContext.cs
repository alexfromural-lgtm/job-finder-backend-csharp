// ============================================================
// FILE: Data/JobFinderDbContext.cs
// CONCEPT: EF Core DbContext — the database gateway
// ------------------------------------------------------------
// DbContext is the central class in Entity Framework Core.
// It:
//   • Holds DbSet<T> properties — one per table you want to query.
//   • Tracks changes to entities and translates C# operations
//     to SQL (SELECT, INSERT, UPDATE, DELETE).
//   • Is configured in Program.cs and injected via DI.
//
// Think of it as "the connection + ORM engine bundled together".
// ============================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Data.Entities;
using JobFinder.Api.Utils;

namespace JobFinder.Api.Data
{
    // 📌 CONCEPT: Inheriting from DbContext
    //    Our class EXTENDS DbContext, so we get all EF machinery
    //    and just configure our specific tables + relationships.
    public class JobFinderDbContext : DbContext
    {
        // 📌 CONCEPT: Constructor with options (DI-friendly pattern)
        //    Program.cs calls:
        //      builder.Services.AddDbContext<JobFinderDbContext>(options =>
        //          options.UseNpgsql(connectionString));
        //    EF Core automatically creates DbContextOptions and passes them here.
        //    We just forward them to the base DbContext.
        public JobFinderDbContext(DbContextOptions<JobFinderDbContext> options) : base(options)
        {
        }

        // ────────────────────────────────────────────────────
        // 📌 CONCEPT: Override SaveChangesAsync to auto-stamp UpdatedAt
        //    Prisma has "@updatedAt" that sets the field on every update.
        //    EF Core has no built-in equivalent, so we override SaveChangesAsync
        //    and set it ourselves before the SQL is executed.
        //
        //    EXAMPLE:
        //      job.Title = "New Title";
        //      await _context.SaveChangesAsync();
        //      // UpdatedAt is automatically set to DateTime.UtcNow
        // ────────────────────────────────────────────────────
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            // 📌 CONCEPT: ChangeTracker
            //    EF Core tracks every entity you loaded or added.
            //    ChangeTracker.Entries() lists them all with their state.
            //    EntityState.Modified means the entity was loaded and changed.
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Modified)
                {
                    // FindProperty returns null if the entity doesn't have UpdatedAt.
                    // This makes the code work for ALL entities (some may not have it).
                    var prop = entry.Metadata.FindProperty("UpdatedAt");
                    if (prop != null)
                    {
                        // Set the column value directly (bypasses C# property setter).
                        entry.Property("UpdatedAt").CurrentValue = now;
                    }
                }
            }

            // Call the original EF SaveChangesAsync which writes the SQL.
            return base.SaveChangesAsync(cancellationToken);
        }

        // ────────────────────────────────────────────────────
        // 📌 CONCEPT: DbSet<T> — query gateway for a table
        //    Each DbSet represents one table.
        //    "=> Set<T>()" is a shorthand for the standard property declaration.
        //
        //    EXAMPLE LINQ queries using DbSets:
        //      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "alice@x.com");
        //      var jobs  = await _context.Jobs.Where(j => j.IsActive).ToListAsync();
        //      _context.Users.Add(newUser);
        //      _context.Jobs.Remove(job);
        // ────────────────────────────────────────────────────
        public DbSet<User> Users => Set<User>();
        public DbSet<JobSeekerProfile> JobSeekerProfiles => Set<JobSeekerProfile>();
        public DbSet<RecruiterProfile> RecruiterProfiles => Set<RecruiterProfile>();
        public DbSet<Job> Jobs => Set<Job>();
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<SavedJob> SavedJobs => Set<SavedJob>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Report> Reports => Set<Report>();

        // ────────────────────────────────────────────────────
        // 📌 CONCEPT: OnModelCreating — Fluent API configuration
        //    This method is called once when EF sets up the model.
        //    It's where you add:
        //      • Enum type registrations (PostgreSQL specific)
        //      • Unique indexes
        //      • Foreign key relationships
        //      • Delete behaviour (Cascade, SetNull, Restrict)
        //
        //    Think of it as the "migration schema blueprint" in C#.
        // ────────────────────────────────────────────────────
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── PostgreSQL Enum registration ─────────────────────────
            // 📌 CONCEPT: PostgreSQL enum types
            //    PostgreSQL can store enum values as typed columns (not integers).
            //    HasPostgresEnum tells Npgsql how to map C# enums ↔ Postgres types.
            //    NpgsqlPrismaNameTranslator keeps type/label naming consistent
            //    with what Prisma originally created.
            //
            //    EXAMPLE: Role.JOB_SEEKER is stored as "job_seeker" text in Postgres.
            var prismaTranslator = NpgsqlPrismaNameTranslator.Instance;
            modelBuilder.HasPostgresEnum<Role>(nameTranslator: prismaTranslator);
            modelBuilder.HasPostgresEnum<ApplicationStatus>(nameTranslator: prismaTranslator);
            modelBuilder.HasPostgresEnum<NotificationType>(nameTranslator: prismaTranslator);
            modelBuilder.HasPostgresEnum<ReportStatus>(nameTranslator: prismaTranslator);

            // ── User ──────────────────────────────────────────────────
            // 📌 CONCEPT: HasIndex(...).IsUnique()
            //    Creates a UNIQUE INDEX on "email" at the database level.
            //    If two users try to register with the same email, the DB
            //    throws a PostgresException (SqlState "23505") which
            //    ErrorHandlerMiddleware converts to HTTP 409.
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // ── JobSeekerProfile (One-to-One with User) ──────────────
            // 📌 CONCEPT: One-to-One relationship
            //    HasOne(A) → WithOne(B) means:
            //      JobSeekerProfile belongs to ONE User.
            //      User has ONE JobSeekerProfile.
            //    HasForeignKey says which column holds the reference (UserId).
            //    OnDelete(Cascade) means: delete the profile when the User is deleted.
            //
            //    EXAMPLE:
            //      DELETE FROM "User" WHERE id = 'u1';
            //      → automatically deletes the matching JobSeekerProfile row.
            modelBuilder.Entity<JobSeekerProfile>(entity =>
            {
                entity.HasIndex(jsp => jsp.UserId).IsUnique(); // one profile per user

                entity.HasOne(jsp => jsp.User)
                    .WithOne(u => u.JobSeeker)
                    .HasForeignKey<JobSeekerProfile>(jsp => jsp.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── RecruiterProfile (One-to-One with User) ──────────────
            modelBuilder.Entity<RecruiterProfile>(entity =>
            {
                entity.HasIndex(rp => rp.UserId).IsUnique();

                entity.HasOne(rp => rp.User)
                    .WithOne(u => u.Recruiter)
                    .HasForeignKey<RecruiterProfile>(rp => rp.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Job ───────────────────────────────────────────────────
            // 📌 CONCEPT: Composite index
            //    "new { j.IsActive, j.Category }" creates one index on TWO columns.
            //    This speeds up queries like:
            //      WHERE IsActive = true AND Category = 'Engineering'
            //
            // 📌 CONCEPT: One-to-Many relationship
            //    HasOne(A) → WithMany(B) means:
            //      A Job belongs to ONE RecruiterProfile.
            //      A RecruiterProfile has MANY Jobs.
            modelBuilder.Entity<Job>(entity =>
            {
                entity.HasIndex(j => j.IsActive);
                entity.HasIndex(j => new { j.IsActive, j.Category });  // composite index
                entity.HasIndex(j => new { j.RecruiterId, j.CreatedAt });
                entity.HasIndex(j => new { j.IsActive, j.CreatedAt });

                entity.HasOne(j => j.Recruiter)
                    .WithMany(rp => rp.Jobs)
                    .HasForeignKey(j => j.RecruiterId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Application ───────────────────────────────────────────
            // 📌 CONCEPT: Unique composite index to prevent duplicates
            //    One job seeker can apply to a job only ONCE.
            //    The composite unique index on (JobId, JobSeekerId) enforces
            //    this at the database level as a last safety net.
            modelBuilder.Entity<Application>(entity =>
            {
                entity.HasIndex(a => new { a.JobId, a.JobSeekerId }).IsUnique();

                entity.HasOne(a => a.Job)
                    .WithMany(j => j.Applications)
                    .HasForeignKey(a => a.JobId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.JobSeeker)
                    .WithMany(jsp => jsp.Applications)
                    .HasForeignKey(a => a.JobSeekerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── SavedJob ──────────────────────────────────────────────
            // Same pattern: one seeker can save a job only once.
            modelBuilder.Entity<SavedJob>(entity =>
            {
                entity.HasIndex(sj => new { sj.JobId, sj.JobSeekerId }).IsUnique();

                entity.HasOne(sj => sj.Job)
                    .WithMany(j => j.SavedBy)
                    .HasForeignKey(sj => sj.JobId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sj => sj.JobSeeker)
                    .WithMany(jsp => jsp.SavedJobs)
                    .HasForeignKey(sj => sj.JobSeekerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Notification ──────────────────────────────────────────
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Report ────────────────────────────────────────────────
            // 📌 CONCEPT: OnDelete(SetNull) vs Cascade
            //    If the reported User or Job is deleted, we keep the Report
            //    but set the foreign key to NULL (useful for audit trails).
            //    The reporter deletion uses Cascade (if reporter account is
            //    removed, their reports go too).
            modelBuilder.Entity<Report>(entity =>
            {
                entity.HasOne(r => r.Reporter)
                    .WithMany(u => u.ReportsMade)
                    .HasForeignKey(r => r.ReporterId)
                    .OnDelete(DeleteBehavior.Cascade);  // delete reporter → delete report

                entity.HasOne(r => r.ReportedUser)
                    .WithMany(u => u.ReportsReceived)
                    .HasForeignKey(r => r.ReportedUserId)
                    .OnDelete(DeleteBehavior.SetNull);  // delete reported user → keep report, null FK

                entity.HasOne(r => r.ReportedJob)
                    .WithMany(j => j.Reports)
                    .HasForeignKey(r => r.ReportedJobId)
                    .OnDelete(DeleteBehavior.SetNull);  // delete reported job → keep report, null FK
            });
        }
    }
}
