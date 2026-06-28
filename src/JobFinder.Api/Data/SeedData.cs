using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Data.Entities;

namespace JobFinder.Api.Data
{
    public static class SeedData
    {
        /// <summary>
        /// Applies any pending EF Core migrations (creates the DB if needed).
        /// Safe to call on every startup — MigrateAsync is idempotent.
        /// </summary>
        public static async Task EnsureDatabaseSchemaAsync(JobFinderDbContext context)
        {
            await context.Database.MigrateAsync();
            Console.WriteLine("✅ EF Core migrations applied successfully.");
        }

        /// <summary>
        /// Clears all tables and repopulates them with representative seed data.
        /// </summary>
        public static async Task InitializeAsync(JobFinderDbContext context)
        {
            Console.WriteLine("🌱 Starting database seed...");

            // Apply migrations before seeding
            await EnsureDatabaseSchemaAsync(context);

            // Clean up existing data for a fresh seed (same order as Prisma seed.ts)
            context.Reports.RemoveRange(context.Reports);
            context.Notifications.RemoveRange(context.Notifications);
            context.SavedJobs.RemoveRange(context.SavedJobs);
            context.Applications.RemoveRange(context.Applications);
            context.Jobs.RemoveRange(context.Jobs);
            context.JobSeekerProfiles.RemoveRange(context.JobSeekerProfiles);
            context.RecruiterProfiles.RemoveRange(context.RecruiterProfiles);
            context.Users.RemoveRange(context.Users);
            await context.SaveChangesAsync();

            // --- Admin User ---
            var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin", 10);
            var admin = new User
            {
                Name = "Admin User",
                Email = "admin@example1.com",
                Password = adminPasswordHash,
                Roles = new List<Role> { Role.ADMIN }
            };
            context.Users.Add(admin);
            Console.WriteLine($"✅ Created admin user: {admin.Email}");

            // --- Recruiter + Profile + Job ---
            var recruiterPasswordHash = BCrypt.Net.BCrypt.HashPassword("recruiter123", 10);
            var recruiterUser = new User
            {
                Name = "Recruiter Jane",
                Email = "recruiter@example.com",
                Password = recruiterPasswordHash,
                Roles = new List<Role> { Role.RECRUITER }
            };
            context.Users.Add(recruiterUser);

            var recruiterProfile = new RecruiterProfile
            {
                User = recruiterUser,
                CompanyName = "Tech Corp",
                CompanyWebsite = "https://techcorp.com",
                Description = "A fast-growing tech company",
                Industry = "Software"
            };
            context.RecruiterProfiles.Add(recruiterProfile);
            Console.WriteLine($"✅ Created recruiter: {recruiterUser.Email}");

            var job = new Job
            {
                Recruiter = recruiterProfile,
                Title = "Senior FullStack Developer",
                Description = "React developer needed",
                Requirements = "8+ years of experience in React and Node.js",
                Location = "Remote",
                SalaryRange = "$130,000 - $180,000",
                Category = "Software"
            };
            context.Jobs.Add(job);
            Console.WriteLine($"✅ Created job: {job.Title}");

            // --- Job Seeker + Profile + Application + SavedJob ---
            var seekerPasswordHash = BCrypt.Net.BCrypt.HashPassword("seeker123", 10);
            var seekerUser = new User
            {
                Name = "Job Seeker John",
                Email = "seeker@example.com",
                Password = seekerPasswordHash,
                Roles = new List<Role> { Role.JOB_SEEKER }
            };
            context.Users.Add(seekerUser);

            var seekerProfile = new JobSeekerProfile
            {
                User = seekerUser,
                Bio = "Passionate about frontend development",
                Location = "Sydney",
                Skills = new List<string> { "React", "TypeScript", "HTML", "CSS" },
                Education = "BSc in Computer Science",
                Experience = "2 years at Webify",
                ResumeUrl = "https://example.com/resume/john.pdf"
            };
            context.JobSeekerProfiles.Add(seekerProfile);
            Console.WriteLine($"✅ Created job seeker: {seekerUser.Email}");

            await context.SaveChangesAsync();

            // --- Create Application ---
            var application = new Application
            {
                Job = job,
                JobSeeker = seekerProfile,
                CoverLetter = "I'm very interested in this opportunity!"
            };
            context.Applications.Add(application);
            Console.WriteLine("✅ Created application");

            // --- Create SavedJob ---
            var savedJob = new SavedJob
            {
                Job = job,
                JobSeeker = seekerProfile
            };
            context.SavedJobs.Add(savedJob);
            Console.WriteLine("✅ Created saved job");

            await context.SaveChangesAsync();
            Console.WriteLine("✅ Seeding complete!");
        }
    }
}
