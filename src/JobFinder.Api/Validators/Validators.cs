using System;
using System.Linq;
using FluentValidation;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Validators
{
    public class JobSeekerSignupValidator : AbstractValidator<JobSeekerSignupDto>
    {
        public JobSeekerSignupValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
            RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email");
            RuleFor(x => x.Password).MinimumLength(6).WithMessage("Password must be at least 6 characters");
        }
    }

    public class RecruiterSignupValidator : AbstractValidator<RecruiterSignupDto>
    {
        public RecruiterSignupValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
            RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email");
            RuleFor(x => x.Password).MinimumLength(6).WithMessage("Password must be at least 6 characters");
            RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Company name is required");
            RuleFor(x => x.CompanyWebsite)
                .Must(uri => string.IsNullOrEmpty(uri) || Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Invalid URL");
            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Description must be at most 1000 characters");
            RuleFor(x => x.Industry).MaximumLength(100).WithMessage("Industry must be at most 100 characters");
        }
    }

    public class LoginValidator : AbstractValidator<LoginDto>
    {
        public LoginValidator()
        {
            RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email");
            RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
        }
    }

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

    public class JobCreateUpdateValidator : AbstractValidator<JobCreateUpdateDto>
    {
        public JobCreateUpdateValidator()
        {
            RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required").MaximumLength(100);
            RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required").MaximumLength(2000);
            RuleFor(x => x.Requirements).NotEmpty().WithMessage("Requirements are required").MaximumLength(2000);
            RuleFor(x => x.Location).NotEmpty().WithMessage("Location is required").MaximumLength(100);
            RuleFor(x => x.SalaryRange).MaximumLength(100);
            RuleFor(x => x.Category).MaximumLength(50);
        }
    }

    public class JobSeekerProfileUpdateValidator : AbstractValidator<JobSeekerProfileUpdateDto>
    {
        public JobSeekerProfileUpdateValidator()
        {
            RuleFor(x => x.Bio).MaximumLength(500).WithMessage("Bio must be at most 500 characters");
            RuleFor(x => x.Location).MaximumLength(100).WithMessage("Location must be at most 100 characters");
            RuleFor(x => x.ResumeUrl)
                .Must(uri => string.IsNullOrEmpty(uri) || Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Invalid URL");
        }
    }

    public class RecruiterProfileUpdateValidator : AbstractValidator<RecruiterProfileUpdateDto>
    {
        public RecruiterProfileUpdateValidator()
        {
            RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Company name is required").When(x => x.CompanyName != null);
            RuleFor(x => x.CompanyWebsite)
                .Must(uri => string.IsNullOrEmpty(uri) || Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Invalid URL")
                .When(x => x.CompanyWebsite != null);
            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Description must be at most 1000 characters");
            RuleFor(x => x.Industry).MaximumLength(100).WithMessage("Industry must be at most 100 characters");
        }
    }

    public class ApplicationCreateValidator : AbstractValidator<ApplicationCreateDto>
    {
        public ApplicationCreateValidator()
        {
            RuleFor(x => x.CoverLetter).MaximumLength(2000).WithMessage("Cover letter must be at most 2000 characters");
        }
    }

    public class ApplicationStatusValidator : AbstractValidator<ApplicationStatusDto>
    {
        private static readonly string[] AllowedStatuses = { "submitted", "shortlisted", "rejected", "under_review" };

        public ApplicationStatusValidator()
        {
            RuleFor(x => x.Status)
                .Must(status => AllowedStatuses.Contains(status))
                .WithMessage("Status must be one of: submitted, shortlisted, rejected, under_review");
        }
    }
}
