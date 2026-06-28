using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Data.Entities;
using JobFinder.Api.Utils;

namespace JobFinder.Api.Data
{
    public class JobFinderDbContext : DbContext
    {
        public JobFinderDbContext(DbContextOptions<JobFinderDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Automatically stamps UpdatedAt on any entity that has that property
        /// before saving, mirroring Prisma's @updatedAt decorator behavior.
        /// </summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Modified)
                {
                    var prop = entry.Metadata.FindProperty("UpdatedAt");
                    if (prop != null)
                    {
                        entry.Property("UpdatedAt").CurrentValue = now;
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<JobSeekerProfile> JobSeekerProfiles => Set<JobSeekerProfile>();
        public DbSet<RecruiterProfile> RecruiterProfiles => Set<RecruiterProfile>();
        public DbSet<Job> Jobs => Set<Job>();
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<SavedJob> SavedJobs => Set<SavedJob>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Report> Reports => Set<Report>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Enum registration ─────────────────────────────────────────────────────
            // NpgsqlPrismaNameTranslator keeps enum TYPE names as PascalCase ("Role")
            // and lowercases enum LABELS ("recruiter", "job_seeker") to match Prisma's
            // PostgreSQL enum conventions.
            var prismaTranslator = NpgsqlPrismaNameTranslator.Instance;
            modelBuilder.HasPostgresEnum<Role>(nameTranslator: prismaTranslator);
            modelBuilder.HasPostgresEnum<ApplicationStatus>(nameTranslator: prismaTranslator);
            modelBuilder.HasPostgresEnum<NotificationType>(nameTranslator: prismaTranslator);
            modelBuilder.HasPostgresEnum<ReportStatus>(nameTranslator: prismaTranslator);

            // ── User ─────────────────────────────────────────────────────────────────
            // Table name and column names are set via [Table] / [Column] data annotations.
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // ── JobSeekerProfile (One-to-One with User) ───────────────────────────────
            modelBuilder.Entity<JobSeekerProfile>(entity =>
            {
                entity.HasIndex(jsp => jsp.UserId).IsUnique();

                entity.HasOne(jsp => jsp.User)
                    .WithOne(u => u.JobSeeker)
                    .HasForeignKey<JobSeekerProfile>(jsp => jsp.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── RecruiterProfile (One-to-One with User) ────────────────────────────────
            modelBuilder.Entity<RecruiterProfile>(entity =>
            {
                entity.HasIndex(rp => rp.UserId).IsUnique();

                entity.HasOne(rp => rp.User)
                    .WithOne(u => u.Recruiter)
                    .HasForeignKey<RecruiterProfile>(rp => rp.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Job ───────────────────────────────────────────────────────────────────
            modelBuilder.Entity<Job>(entity =>
            {
                // Indices matching the original Prisma schema for query performance
                entity.HasIndex(j => j.IsActive);
                entity.HasIndex(j => new { j.IsActive, j.Category });
                entity.HasIndex(j => new { j.RecruiterId, j.CreatedAt });
                entity.HasIndex(j => new { j.IsActive, j.CreatedAt });

                entity.HasOne(j => j.Recruiter)
                    .WithMany(rp => rp.Jobs)
                    .HasForeignKey(j => j.RecruiterId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Application ────────────────────────────────────────────────────────────
            modelBuilder.Entity<Application>(entity =>
            {
                // Enforce one application per job per seeker at the DB level
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

            // ── SavedJob ───────────────────────────────────────────────────────────────
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

            // ── Notification ───────────────────────────────────────────────────────────
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Report ─────────────────────────────────────────────────────────────────
            modelBuilder.Entity<Report>(entity =>
            {
                // Reporter (required User)
                entity.HasOne(r => r.Reporter)
                    .WithMany(u => u.ReportsMade)
                    .HasForeignKey(r => r.ReporterId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Reported User (optional)
                entity.HasOne(r => r.ReportedUser)
                    .WithMany(u => u.ReportsReceived)
                    .HasForeignKey(r => r.ReportedUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Reported Job (optional)
                entity.HasOne(r => r.ReportedJob)
                    .WithMany(j => j.Reports)
                    .HasForeignKey(r => r.ReportedJobId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
