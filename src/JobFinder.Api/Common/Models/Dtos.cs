// ============================================================
// FILE: Common/Models/Dtos.cs
// CONCEPT: Data Transfer Objects (DTOs)
// ------------------------------------------------------------
// DTOs are simple "bag of properties" classes used to move
// data into or out of the API.
//
//   • REQUEST DTOs  — shape of the JSON body the client sends.
//   • RESPONSE DTOs — shape of the JSON body the server returns.
//
// Benefits over using entity classes directly:
//   ✔ Hides internal fields (e.g. hashed passwords).
//   ✔ Lets you rename/reshape the API surface independently.
//   ✔ Gives validators a clean target class to work with.
// ============================================================

using System;
using System.Collections.Generic;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Common.Models
{
    // ════════════════════════════════════════════════════════
    //  AUTH — REQUEST DTOs
    // ════════════════════════════════════════════════════════

    // 📌 CONCEPT: Request DTO
    //    ASP.NET Core reads the JSON body and fills this class
    //    automatically when you write [FromBody] JobSeekerSignupDto dto.
    //
    //    EXAMPLE JSON the client must send to POST /api/auth/signup/jobseeker:
    //    {
    //      "name": "Alice",
    //      "email": "alice@example.com",
    //      "password": "secret123"
    //    }
    public class JobSeekerSignupDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // 📌 CONCEPT: Optional properties with nullable type (string?)
    //    "string?" means the property may be null — it is NOT required.
    //    "string" (without ?) means it is always present (never null).
    //
    //    EXAMPLE JSON for POST /api/auth/signup/recruiter:
    //    {
    //      "name": "Bob",
    //      "email": "bob@acme.com",
    //      "password": "secret123",
    //      "companyName": "ACME Corp",
    //      "industry": "Technology"   ← optional, can be omitted
    //    }
    public class RecruiterSignupDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyWebsite { get; set; }   // optional
        public string? Description { get; set; }       // optional
        public string? Industry { get; set; }          // optional
    }

    // 📌 CONCEPT: Minimal DTO — only the fields needed for a particular operation
    //    POST /api/auth/login only needs email + password.
    //    Having a separate class prevents accidentally accepting extra fields.
    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // A job-seeker upgrading to a recruiter must supply their company info.
    // POST /api/auth/upgrade/recruiter
    public class RecruiterUpgradeDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyWebsite { get; set; }
        public string? Description { get; set; }
        public string? Industry { get; set; }
    }

    // ════════════════════════════════════════════════════════
    //  AUTH — RESPONSE DTO
    // ════════════════════════════════════════════════════════

    // 📌 CONCEPT: Response DTO — data the server sends BACK
    //    After login/signup the server also sets HttpOnly cookies,
    //    but it still returns the token strings for clients that
    //    prefer to store them in memory (e.g. mobile apps).
    //
    //    EXAMPLE JSON response:
    //    {
    //      "accessToken":  "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    //      "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    //    }
    public class TokenResultDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    // ════════════════════════════════════════════════════════
    //  JOB — REQUEST DTO
    // ════════════════════════════════════════════════════════

    // 📌 CONCEPT: One DTO reused for both CREATE and UPDATE
    //    POST /api/jobs  (create) and PUT /api/jobs/{id} (update)
    //    accept exactly the same fields, so we share the class.
    //
    //    EXAMPLE JSON:
    //    {
    //      "title": "Senior .NET Developer",
    //      "description": "Build great APIs …",
    //      "requirements": "5+ years C# experience …",
    //      "location": "Remote",
    //      "salaryRange": "$80k–$120k",
    //      "category": "Engineering"
    //    }
    public class JobCreateUpdateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }  // optional — some jobs don't disclose salary
        public string? Category { get; set; }     // optional — e.g. "Engineering", "Design"
    }

    // ════════════════════════════════════════════════════════
    //  PROFILE UPDATE — REQUEST DTOs
    // ════════════════════════════════════════════════════════

    // 📌 CONCEPT: Partial update DTO — ALL fields nullable
    //    The client only sends the fields it wants to change.
    //    Fields left null are ignored in the service layer.
    //
    //    EXAMPLE JSON (PATCH-style update — only update bio):
    //    { "bio": "Passionate about cloud engineering." }
    public class JobSeekerProfileUpdateDto
    {
        public string? Bio { get; set; }
        public string? Location { get; set; }
        public List<string>? Skills { get; set; }   // e.g. ["C#", "Docker", "PostgreSQL"]
        public string? Education { get; set; }
        public string? Experience { get; set; }
        public string? ResumeUrl { get; set; }
    }

    public class RecruiterProfileUpdateDto
    {
        public string? CompanyName { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? Description { get; set; }
        public string? Industry { get; set; }
    }

    // ════════════════════════════════════════════════════════
    //  APPLICATION — REQUEST DTOs
    // ════════════════════════════════════════════════════════

    // POST /api/jobs/{jobId}/apply — body may be empty or contain a cover letter.
    public class ApplicationCreateDto
    {
        public string? CoverLetter { get; set; }  // optional — max 2000 chars (see Validators)
    }

    // PUT /api/jobs/{jobId}/applications/{appId}/status (recruiter only)
    // EXAMPLE JSON: { "status": "shortlisted" }
    public class ApplicationStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    // ════════════════════════════════════════════════════════
    //  RESPONSE DTOs
    // ════════════════════════════════════════════════════════

    // 📌 CONCEPT: Response DTO that OMITS sensitive data
    //    Notice: no "Password" field — we never expose the hashed password.
    //
    //    EXAMPLE JSON for GET /api/auth/me:
    //    {
    //      "id": "a1b2c3",
    //      "name": "Alice",
    //      "email": "alice@example.com",
    //      "roles": ["JOB_SEEKER"],
    //      "isActive": true,
    //      "createdAt": "2026-01-15T10:00:00Z",
    //      "updatedAt": "2026-06-01T08:30:00Z"
    //    }
    public class UserResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<Role> Roles { get; set; } = new();  // 📌 new() = new List<Role>() (C# 9 target-typed new)
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Wraps user inside "user" key: { "user": { ... } }
    // Used by the /me endpoint wrapper object.
    public class UserMeResponseDto
    {
        public UserResponseDto User { get; set; } = null!; // 📌 null! = "I know this may look null but it will always be set before use"
    }

    // ────────────────────────────────────────────────────────
    // RECRUITER PROFILE
    // ────────────────────────────────────────────────────────

    // 📌 CONCEPT: Nested DTOs — RecruiterProfileResponseDto contains a UserResponseDto
    //    This avoids a full "include everything" query and shapes the
    //    response to exactly what the frontend needs.
    //
    //    EXAMPLE JSON:
    //    {
    //      "id": "rp-1",
    //      "companyName": "ACME",
    //      "user": { "name": "Bob", ... }
    //    }
    public class RecruiterProfileResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyWebsite { get; set; }
        public string? Description { get; set; }
        public string? Industry { get; set; }
        public UserResponseDto User { get; set; } = null!;
    }

    // ────────────────────────────────────────────────────────
    // JOB SEEKER PROFILE
    // ────────────────────────────────────────────────────────
    public class JobSeekerProfileResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Location { get; set; }
        public List<string> Skills { get; set; } = new();  // e.g. ["C#", "React"]
        public string? Education { get; set; }
        public string? Experience { get; set; }
        public string? ResumeUrl { get; set; }
        public UserResponseDto User { get; set; } = null!;
    }

    // ────────────────────────────────────────────────────────
    // JOB RESPONSE — full detail for GET /api/jobs/{id}
    // ────────────────────────────────────────────────────────
    public class JobResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string RecruiterId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // 📌 CONCEPT: Nested DTO for the recruiter's company info
        //    Only public-safe fields — not the full RecruiterProfile entity.
        public RecruiterCompanyDto? Recruiter { get; set; }
    }

    // A slim "company info" DTO embedded inside job responses.
    public class RecruiterCompanyDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string? CompanyWebsite { get; set; }
    }

    // ────────────────────────────────────────────────────────
    // PAGINATED JOB SEARCH RESULT — GET /api/jobs/all
    // ────────────────────────────────────────────────────────

    // 📌 CONCEPT: Pagination metadata pattern
    //    API returns both the page of data AND metadata so the UI
    //    can render "Page 2 of 7 (68 results)".
    //
    //    EXAMPLE JSON:
    //    {
    //      "jobs": [ { "id": "...", "title": "..." }, ... ],
    //      "meta": { "total": 68, "page": 2, "pageSize": 10, "totalPages": 7 }
    //    }
    public class JobSearchResultDto
    {
        public List<JobResponseDto> Jobs { get; set; } = new();
        public JobsMetaDto Meta { get; set; } = new();
    }

    public class JobsMetaDto
    {
        public int Total { get; set; }       // total matching records across ALL pages
        public int Page { get; set; }        // current page number (1-based)
        public int PageSize { get; set; }    // records per page
        public int TotalPages { get; set; } // = ceil(Total / PageSize)
    }

    // ────────────────────────────────────────────────────────
    // APPLICATION RESPONSES
    // ────────────────────────────────────────────────────────

    // 📌 CONCEPT: "View from the job-seeker's side"
    //    Includes job info so the seeker can see which job they applied to.
    public class ApplicationResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string JobSeekerId { get; set; } = string.Empty;
        public string? CoverLetter { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public JobShortDto Job { get; set; } = null!;  // embedded job summary
    }

    // Slim job summary embedded inside application responses for seekers.
    public class JobShortDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public string? Category { get; set; }
        public RecruiterNameDto Recruiter { get; set; } = null!;
    }

    // Even slimmer: only the company name.
    public class RecruiterNameDto
    {
        public string CompanyName { get; set; } = string.Empty;
    }

    // 📌 CONCEPT: "View from the recruiter's side"
    //    Includes job-seeker info so the recruiter can evaluate candidates.
    public class RecruiterApplicationResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string JobSeekerId { get; set; } = string.Empty;
        public string? CoverLetter { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public JobTitleDto Job { get; set; } = null!;
        public JobSeekerShortDto JobSeeker { get; set; } = null!;  // candidate summary
    }

    // Only the job title needed in recruiter's application list.
    public class JobTitleDto
    {
        public string Title { get; set; } = string.Empty;
    }

    // Candidate info embedded in the recruiter view.
    public class JobSeekerShortDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Location { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? Education { get; set; }
        public string? Experience { get; set; }
        public string? ResumeUrl { get; set; }
        public UserShortDto User { get; set; } = null!;
    }

    // Minimal user fields for embedding.
    public class UserShortDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    // ────────────────────────────────────────────────────────
    // SAVED JOB RESPONSE
    // ────────────────────────────────────────────────────────
    public class SavedJobResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string JobSeekerId { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
        public JobShortActiveDto Job { get; set; } = null!;
    }

    // Like JobShortDto but also includes IsActive so the UI can
    // flag jobs that are no longer accepting applications.
    public class JobShortActiveDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; }  // ← extra field vs JobShortDto
        public RecruiterNameDto Recruiter { get; set; } = null!;
    }
}
