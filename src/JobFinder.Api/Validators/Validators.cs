// ============================================================
// FILE: Validators/Validators.cs
// CONCEPT: FluentValidation — declarative input validation
// ------------------------------------------------------------
// FluentValidation lets you define validation rules in a fluent,
// readable way using a chain of method calls.
//
// Benefits over Data Annotations ([Required], [MaxLength]):
//   ✔ Rules are in one dedicated class (not scattered on the DTO)
//   ✔ Complex rules (custom predicates, conditional rules) are easy
//   ✔ Testable independently
//   ✔ Rich error messages per rule
//
// Each validator class:
//   1. Inherits from AbstractValidator<T>
//   2. Calls RuleFor(...) in the constructor to register rules
//   3. Is registered in DI as IValidator<T> in Program.cs
// ============================================================

using System;
using System.Linq;
using FluentValidation;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Validators
{
    // ────────────────────────────────────────────────────────
    // JOB SEEKER SIGNUP
    // ────────────────────────────────────────────────────────

    // 📌 CONCEPT: AbstractValidator<T>
    //    Inherit from AbstractValidator<JobSeekerSignupDto> to build
    //    a validator for that specific DTO type.
    //    All rules are registered in the constructor.
    public class JobSeekerSignupValidator : AbstractValidator<JobSeekerSignupDto>
    {
        public JobSeekerSignupValidator()
        {
            // 📌 CONCEPT: RuleFor(lambda).ValidationMethod().WithMessage(...)
            //    "x => x.Name" selects the property to validate.
            //    "NotEmpty()" fails if the value is null, empty, or whitespace.
            //    "WithMessage(...)" overrides the default error message.
            //
            //    EXAMPLE: dto.Name = ""
            //      → IsValid = false, ErrorMessage = "Name is required"
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");

            // 📌 CONCEPT: .EmailAddress()
            //    Checks the value looks like a valid email (contains @, has domain, etc.)
            //    EXAMPLE: "not-an-email" → "Invalid email"
            //             "alice@x.com"  → passes
            RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email");

            // 📌 CONCEPT: .MinimumLength(6)
            //    Rejects passwords shorter than 6 characters.
            //    EXAMPLE: "abc" → "Password must be at least 6 characters"
            //             "secret123" → passes
            RuleFor(x => x.Password).MinimumLength(6).WithMessage("Password must be at least 6 characters");
        }
    }

    // ────────────────────────────────────────────────────────
    // RECRUITER SIGNUP
    // ────────────────────────────────────────────────────────
    public class RecruiterSignupValidator : AbstractValidator<RecruiterSignupDto>
    {
        public RecruiterSignupValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
            RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email");
            RuleFor(x => x.Password).MinimumLength(6).WithMessage("Password must be at least 6 characters");
            RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Company name is required");

            // 📌 CONCEPT: .Must(predicate) — custom validation logic
            //    "Must" accepts a lambda that returns bool.
            //    Here: allow null/empty (optional) OR a valid absolute URI.
            //
            //    EXAMPLE valid values:
            //      null                       → passes (optional field)
            //      ""                         → passes (empty = not provided)
            //      "https://acme.com"         → passes (valid URI)
            //    EXAMPLE invalid:
            //      "not-a-url"                → "Invalid URL"
            //      "ftp://"                   → "Invalid URL" (not absolute)
            RuleFor(x => x.CompanyWebsite)
                .Must(uri => string.IsNullOrEmpty(uri) || Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Invalid URL");

            // 📌 CONCEPT: .MaximumLength(N) — cap text field length
            //    Prevents excessive data storage and UI overflow.
            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Description must be at most 1000 characters");
            RuleFor(x => x.Industry).MaximumLength(100).WithMessage("Industry must be at most 100 characters");
        }
    }

    // ────────────────────────────────────────────────────────
    // LOGIN
    // ────────────────────────────────────────────────────────
    public class LoginValidator : AbstractValidator<LoginDto>
    {
        public LoginValidator()
        {
            RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email");
            RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
        }
    }

    // ────────────────────────────────────────────────────────
    // RECRUITER UPGRADE (JOB_SEEKER → also RECRUITER)
    // ────────────────────────────────────────────────────────
    public class RecruiterUpgradeValidator : AbstractValidator<RecruiterUpgradeDto>
    {
        public RecruiterUpgradeValidator()
        {
            RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Company name is required");
            RuleFor(x => x.CompanyWebsite)
                .Must(uri => string.IsNullOrEmpty(uri) || Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Invalid URL");
            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Description must be at most 1000 characters");
            RuleFor(x => x.Industry).MaximumLength(100).WithMessage("Industry must be at most 100 characters");
        }
    }

    // ────────────────────────────────────────────────────────
    // JOB CREATE / UPDATE
    // ────────────────────────────────────────────────────────

    // 📌 CONCEPT: Chaining multiple rules on one property
    //    .NotEmpty().MaximumLength(100) means BOTH rules apply.
    //    If the title is empty, the first error fires.
    //    If it's 150 chars (not empty but too long), the second fires.
    public class JobCreateUpdateValidator : AbstractValidator<JobCreateUpdateDto>
    {
        public JobCreateUpdateValidator()
        {
            RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required").MaximumLength(100);
            RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required").MaximumLength(2000);
            RuleFor(x => x.Requirements).NotEmpty().WithMessage("Requirements are required").MaximumLength(2000);
            RuleFor(x => x.Location).NotEmpty().WithMessage("Location is required").MaximumLength(100);
            RuleFor(x => x.SalaryRange).MaximumLength(100);  // optional — no NotEmpty
            RuleFor(x => x.Category).MaximumLength(50);      // optional
        }
    }

    // ────────────────────────────────────────────────────────
    // JOB SEEKER PROFILE UPDATE
    // ────────────────────────────────────────────────────────
    public class JobSeekerProfileUpdateValidator : AbstractValidator<JobSeekerProfileUpdateDto>
    {
        public JobSeekerProfileUpdateValidator()
        {
            // All fields are optional on a profile update, but have length limits.
            RuleFor(x => x.Bio).MaximumLength(500).WithMessage("Bio must be at most 500 characters");
            RuleFor(x => x.Location).MaximumLength(100).WithMessage("Location must be at most 100 characters");
            RuleFor(x => x.ResumeUrl)
                .Must(uri => string.IsNullOrEmpty(uri) || Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Invalid URL");
        }
    }

    // ────────────────────────────────────────────────────────
    // RECRUITER PROFILE UPDATE
    // ────────────────────────────────────────────────────────
    public class RecruiterProfileUpdateValidator : AbstractValidator<RecruiterProfileUpdateDto>
    {
        public RecruiterProfileUpdateValidator()
        {
            // 📌 CONCEPT: .When(condition) — conditional rule
            //    The CompanyName rule only runs when the field is NOT null.
            //    If the client omits CompanyName (null) it's not validated.
            //    If they DO send it, it must be non-empty.
            //
            //    EXAMPLE:
            //      { }                              → passes (not changing company name)
            //      { "companyName": "" }            → "Company name is required"
            //      { "companyName": "ACME Corp" }   → passes
            RuleFor(x => x.CompanyName)
                .NotEmpty().WithMessage("Company name is required")
                .When(x => x.CompanyName != null);

            RuleFor(x => x.CompanyWebsite)
                .Must(uri => string.IsNullOrEmpty(uri) || Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Invalid URL")
                .When(x => x.CompanyWebsite != null);

            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Description must be at most 1000 characters");
            RuleFor(x => x.Industry).MaximumLength(100).WithMessage("Industry must be at most 100 characters");
        }
    }

    // ────────────────────────────────────────────────────────
    // APPLICATION CREATE
    // ────────────────────────────────────────────────────────
    public class ApplicationCreateValidator : AbstractValidator<ApplicationCreateDto>
    {
        public ApplicationCreateValidator()
        {
            // CoverLetter is optional but can't be excessively long.
            RuleFor(x => x.CoverLetter).MaximumLength(2000).WithMessage("Cover letter must be at most 2000 characters");
        }
    }

    // ────────────────────────────────────────────────────────
    // APPLICATION STATUS UPDATE (Recruiter changes status)
    // ────────────────────────────────────────────────────────
    public class ApplicationStatusValidator : AbstractValidator<ApplicationStatusDto>
    {
        // 📌 CONCEPT: Static readonly array for allowed values (whitelist)
        //    Using a whitelist is safer than trying to block disallowed values.
        //    Any value not in this list is rejected.
        private static readonly string[] AllowedStatuses = { "submitted", "shortlisted", "rejected", "under_review" };

        public ApplicationStatusValidator()
        {
            // 📌 CONCEPT: .Contains() inside .Must() for whitelist validation
            //    EXAMPLE:
            //      { "status": "shortlisted" } → passes
            //      { "status": "approved" }    → "Status must be one of: ..."
            //      { "status": "" }            → "Status must be one of: ..."
            RuleFor(x => x.Status)
                .Must(status => AllowedStatuses.Contains(status))
                .WithMessage("Status must be one of: submitted, shortlisted, rejected, under_review");
        }
    }
}
