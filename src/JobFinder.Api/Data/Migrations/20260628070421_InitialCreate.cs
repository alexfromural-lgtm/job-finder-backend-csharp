using System;
using System.Collections.Generic;
using JobFinder.Api.Common.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobFinder.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ApplicationStatus", "submitted,shortlisted,rejected,under_review")
                .Annotation("Npgsql:Enum:NotificationType", "application_update,system")
                .Annotation("Npgsql:Enum:ReportStatus", "open,reviewed,dismissed")
                .Annotation("Npgsql:Enum:Role", "job_seeker,recruiter,admin");

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: false),
                    roles = table.Column<List<Role>>(type: "\"Role\"[]", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "JobSeekerProfile",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    bio = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    skills = table.Column<List<string>>(type: "text[]", nullable: false),
                    education = table.Column<string>(type: "text", nullable: true),
                    experience = table.Column<string>(type: "text", nullable: true),
                    resumeUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSeekerProfile", x => x.id);
                    table.ForeignKey(
                        name: "FK_JobSeekerProfile_User_userId",
                        column: x => x.userId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<NotificationType>(type: "\"NotificationType\"", nullable: false),
                    isRead = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.id);
                    table.ForeignKey(
                        name: "FK_Notification_User_userId",
                        column: x => x.userId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecruiterProfile",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    companyName = table.Column<string>(type: "text", nullable: false),
                    companyWebsite = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    industry = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruiterProfile", x => x.id);
                    table.ForeignKey(
                        name: "FK_RecruiterProfile_User_userId",
                        column: x => x.userId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Job",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    recruiterId = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    requirements = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    salaryRange = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "text", nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Job", x => x.id);
                    table.ForeignKey(
                        name: "FK_Job_RecruiterProfile_recruiterId",
                        column: x => x.recruiterId,
                        principalTable: "RecruiterProfile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Application",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    jobId = table.Column<string>(type: "text", nullable: false),
                    jobSeekerId = table.Column<string>(type: "text", nullable: false),
                    coverLetter = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<ApplicationStatus>(type: "\"ApplicationStatus\"", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Application", x => x.id);
                    table.ForeignKey(
                        name: "FK_Application_JobSeekerProfile_jobSeekerId",
                        column: x => x.jobSeekerId,
                        principalTable: "JobSeekerProfile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Application_Job_jobId",
                        column: x => x.jobId,
                        principalTable: "Job",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Report",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    reporterId = table.Column<string>(type: "text", nullable: false),
                    reportedUserId = table.Column<string>(type: "text", nullable: true),
                    reportedJobId = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<ReportStatus>(type: "\"ReportStatus\"", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Report", x => x.id);
                    table.ForeignKey(
                        name: "FK_Report_Job_reportedJobId",
                        column: x => x.reportedJobId,
                        principalTable: "Job",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Report_User_reportedUserId",
                        column: x => x.reportedUserId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Report_User_reporterId",
                        column: x => x.reporterId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedJob",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    jobId = table.Column<string>(type: "text", nullable: false),
                    jobSeekerId = table.Column<string>(type: "text", nullable: false),
                    savedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedJob", x => x.id);
                    table.ForeignKey(
                        name: "FK_SavedJob_JobSeekerProfile_jobSeekerId",
                        column: x => x.jobSeekerId,
                        principalTable: "JobSeekerProfile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedJob_Job_jobId",
                        column: x => x.jobId,
                        principalTable: "Job",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Application_jobId_jobSeekerId",
                table: "Application",
                columns: new[] { "jobId", "jobSeekerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Application_jobSeekerId",
                table: "Application",
                column: "jobSeekerId");

            migrationBuilder.CreateIndex(
                name: "IX_Job_isActive",
                table: "Job",
                column: "isActive");

            migrationBuilder.CreateIndex(
                name: "IX_Job_isActive_category",
                table: "Job",
                columns: new[] { "isActive", "category" });

            migrationBuilder.CreateIndex(
                name: "IX_Job_isActive_createdAt",
                table: "Job",
                columns: new[] { "isActive", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Job_recruiterId_createdAt",
                table: "Job",
                columns: new[] { "recruiterId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobSeekerProfile_userId",
                table: "JobSeekerProfile",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_userId",
                table: "Notification",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterProfile_userId",
                table: "RecruiterProfile",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Report_reportedJobId",
                table: "Report",
                column: "reportedJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_reportedUserId",
                table: "Report",
                column: "reportedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_reporterId",
                table: "Report",
                column: "reporterId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedJob_jobId_jobSeekerId",
                table: "SavedJob",
                columns: new[] { "jobId", "jobSeekerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedJob_jobSeekerId",
                table: "SavedJob",
                column: "jobSeekerId");

            migrationBuilder.CreateIndex(
                name: "IX_User_email",
                table: "User",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Application");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "Report");

            migrationBuilder.DropTable(
                name: "SavedJob");

            migrationBuilder.DropTable(
                name: "JobSeekerProfile");

            migrationBuilder.DropTable(
                name: "Job");

            migrationBuilder.DropTable(
                name: "RecruiterProfile");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
