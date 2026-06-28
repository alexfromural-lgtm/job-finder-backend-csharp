namespace JobFinder.Api.Common.Models
{
    public enum Role
    {
        JOB_SEEKER,
        RECRUITER,
        ADMIN
    }

    public enum ApplicationStatus
    {
        submitted,
        shortlisted,
        rejected,
        under_review
    }

    public enum NotificationType
    {
        application_update,
        system
    }

    public enum ReportStatus
    {
        open,
        reviewed,
        dismissed
    }
}
