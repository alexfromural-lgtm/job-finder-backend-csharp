// ============================================================
// FILE: Common/Models/Enums.cs
// CONCEPT: Enumerations (enums)
// ------------------------------------------------------------
// An enum is a named set of integer constants.
// Instead of using raw strings like "JOB_SEEKER" everywhere,
// we use an enum so the compiler catches typos and IDEs give
// auto-completion.
//
// These enums are also mapped to PostgreSQL enum TYPES by
// Npgsql (see DbContext) so the database stores them as
// human-readable labels (e.g. "job_seeker") instead of 0/1/2.
// ============================================================

namespace JobFinder.Api.Common.Models
{
    // 📌 CONCEPT: enum
    //    Each name gets an integer value starting at 0 unless specified.
    //      Role.JOB_SEEKER == 0
    //      Role.RECRUITER  == 1
    //      Role.ADMIN      == 2
    //
    //    EXAMPLE usage in code:
    //      var user = new User { Roles = new List<Role> { Role.JOB_SEEKER } };
    //      if (user.Roles.Contains(Role.RECRUITER)) { ... }
    //
    //    EXAMPLE usage in attribute:
    //      [AuthorizeRoles(Role.RECRUITER)]   // only recruiters may call this endpoint
    public enum Role
    {
        JOB_SEEKER,  // 0 — a candidate looking for work
        RECRUITER,   // 1 — a company posting jobs
        ADMIN        // 2 — platform administrator (future use)
    }

    // 📌 CONCEPT: lowercase enum member names
    //    C# enum member names are normally PascalCase, but here they are
    //    lowercase to match the PostgreSQL enum labels created by Prisma.
    //    NpgsqlPrismaNameTranslator (see Utils/) maps them 1-to-1.
    //
    //    EXAMPLE lifecycle:
    //      1. Job seeker applies → Status = ApplicationStatus.submitted
    //      2. Recruiter reviews  → Status = ApplicationStatus.under_review
    //      3. Recruiter shortlists → Status = ApplicationStatus.shortlisted
    //         or rejects          → Status = ApplicationStatus.rejected
    public enum ApplicationStatus
    {
        submitted,    // just applied
        shortlisted,  // recruiter likes the candidate
        rejected,     // recruiter said no
        under_review  // recruiter is evaluating
    }

    // 📌 CONCEPT: Enums for notification types
    //    Keeps notification-type checks type-safe.
    //
    //    EXAMPLE:
    //      var n = new Notification { Type = NotificationType.application_update };
    //      if (n.Type == NotificationType.system) { sendEmail(...); }
    public enum NotificationType
    {
        application_update,  // e.g. "Your application was shortlisted"
        system               // platform-wide announcements
    }

    // 📌 CONCEPT: Enum for report/moderation status
    //
    //    EXAMPLE flow:
    //      Report submitted → open
    //      Admin reviews    → reviewed  (or dismissed if false alarm)
    public enum ReportStatus
    {
        open,       // new, needs admin attention
        reviewed,   // admin has taken action
        dismissed   // admin decided it was not a real issue
    }
}
