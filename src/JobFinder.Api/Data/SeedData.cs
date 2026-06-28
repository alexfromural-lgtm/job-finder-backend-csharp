using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using JobFinder.Api.Common.Models;
using JobFinder.Api.Data.Entities;

namespace JobFinder.Api.Data
{
    public static class SeedData
    {
        public static async Task EnsureDatabaseSchemaAsync(JobFinderDbContext context)
        {
            var databaseCreator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
            if (databaseCreator != null)
            {
                if (!await databaseCreator.ExistsAsync())
                {
                    await databaseCreator.CreateAsync();
                }

                bool tablesExist = false;
                try
                {
                    await context.Users.AnyAsync();
                    tablesExist = true;
                }
                catch
                {
                    // Tables do not exist
                }

                if (!tablesExist)
                {
                    await databaseCreator.CreateTablesAsync();
                    Console.WriteLine("✅ Database schema initialized and tables created.");
                }
            }
        }

        public static async Task InitializeAsync(JobFinderDbContext context)
        {
            Console.WriteLine("🌱 Starting database seed...");

            // Ensure database is created and schema is initialized
            await EnsureDatabaseSchemaAsync(context);

            // Check if database is already seeded (avoid double-seeding if we just want a simple check,
            // but prisma/seed.ts cleans up first, so let's do the same for a fresh seed experience)
            
            // Clean up existing data for a fresh seed
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
