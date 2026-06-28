using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Data.Entities;

namespace JobFinder.Api.Data
{
    public class JobFinderDbContext : DbContext
    {
        public JobFinderDbContext(DbContextOptions<JobFinderDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Automatically stamps UpdatedAt on any entity that has that property
        /// before saving, matching Prisma's @updatedAt decorator behavior.
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

            // Register PostgreSQL Enums — names must match the Prisma-generated enum type names exactly
            modelBuilder.HasPostgresEnum<Role>("public", "Role");
            modelBuilder.HasPostgresEnum<ApplicationStatus>("public", "ApplicationStatus");
            modelBuilder.HasPostgresEnum<NotificationType>("public", "NotificationType");
            modelBuilder.HasPostgresEnum<ReportStatus>("public", "ReportStatus");

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Name).IsRequired();
                entity.Property(u => u.Email).IsRequired();
                entity.Property(u => u.Password).IsRequired();
                entity.Property(u => u.Roles).IsRequired(); // List<Role> mapped to Role[]
            });

            // JobSeekerProfile configuration (One-to-One with User)
            modelBuilder.Entity<JobSeekerProfile>(entity =>
            {
                entity.HasKey(jsp => jsp.Id);
                entity.HasIndex(jsp => jsp.UserId).IsUnique();
                
                entity.HasOne(jsp => jsp.User)
                    .WithOne(u => u.JobSeeker)
                    .HasForeignKey<JobSeekerProfile>(jsp => jsp.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // RecruiterProfile configuration (One-to-One with User)
            modelBuilder.Entity<RecruiterProfile>(entity =>
            {
                entity.HasKey(rp => rp.Id);
                entity.HasIndex(rp => rp.UserId).IsUnique();

                entity.HasOne(rp => rp.User)
                    .WithOne(u => u.Recruiter)
                    .HasForeignKey<RecruiterProfile>(rp => rp.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Job configuration
            modelBuilder.Entity<Job>(entity =>
            {
                entity.HasKey(j => j.Id);
                
                // Add indices matching schema.prisma
                entity.HasIndex(j => j.IsActive);
                entity.HasIndex(j => new { j.IsActive, j.Category });
                entity.HasIndex(j => new { j.RecruiterId, j.CreatedAt });
                entity.HasIndex(j => new { j.IsActive, j.CreatedAt });

                entity.HasOne(j => j.Recruiter)
                    .WithMany(rp => rp.Jobs)
                    .HasForeignKey(j => j.RecruiterId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Application configuration
            modelBuilder.Entity<Application>(entity =>
            {
                entity.HasKey(a => a.Id);

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

            // SavedJob configuration
            modelBuilder.Entity<SavedJob>(entity =>
            {
                entity.HasKey(sj => sj.Id);
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

            // Notification configuration
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.HasOne(n => n.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Report configuration
            modelBuilder.Entity<Report>(entity =>
            {
                entity.HasKey(r => r.Id);

                // Reporter (User)
                entity.HasOne(r => r.Reporter)
                    .WithMany(u => u.ReportsMade)
                    .HasForeignKey(r => r.ReporterId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Reported User (User? - optional)
                entity.HasOne(r => r.ReportedUser)
                    .WithMany(u => u.ReportsReceived)
                    .HasForeignKey(r => r.ReportedUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Reported Job (Job? - optional)
                entity.HasOne(r => r.ReportedJob)
                    .WithMany(j => j.Reports)
                    .HasForeignKey(r => r.ReportedJobId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
