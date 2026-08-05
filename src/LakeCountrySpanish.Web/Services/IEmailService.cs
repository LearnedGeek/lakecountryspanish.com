using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);

    /// <summary>
    /// Wraps <paramref name="bodyContentHtml"/> in the LCS-branded shell (logo
    /// header, gradient title band, white card, footer) and sends it. Any
    /// caller composing transactional email should prefer this over
    /// <see cref="SendEmailAsync"/> so the visual language stays consistent
    /// across the whole site.
    /// </summary>
    /// <param name="headerTitle">Title text shown in the gradient band at the top of the card.</param>
    /// <param name="headerColorHex">Base color for the gradient (e.g. "#1E3A8A" for navy).</param>
    /// <param name="bodyContentHtml">Pre-composed HTML to drop inside the card body.</param>
    /// <param name="preheader">Optional invisible summary line Gmail shows next to the subject.</param>
    /// <param name="emoji">Optional emoji prefixed to the header title.</param>
    Task SendBrandedEmailAsync(string toEmail, string toName, string subject,
        string headerTitle, string headerColorHex, string bodyContentHtml,
        string? preheader = null, string? emoji = null);
    Task SendClassScheduledAsync(string studentEmail, string studentName, DateTime classDateTime, string? classroomUrl);
    Task SendClassRescheduledAsync(string studentEmail, string studentName, DateTime oldDateTime, DateTime newDateTime, string? reason);
    Task SendClassCancelledAsync(string studentEmail, string studentName, DateTime classDateTime, string? reason);

    // Notification emails
    Task SendClassReminderAsync(string email, string name, DateTime classDateTime, string? classroomUrl, int hoursUntil);
    Task SendPointMilestoneAsync(string email, string name, int totalPoints, int milestone, int tokensEarned);
    Task SendBadgeEarnedAsync(string email, string name, string badgeName, string description, int bonusPoints);
    Task SendPaymentConfirmationAsync(string email, string name, decimal amount, string description, DateTime paymentDate);
    Task SendSubscriptionRenewalAsync(string email, string name, string tierName, decimal amount, DateTime nextRenewalDate);
    Task SendAssignmentAssignedAsync(string email, string name, string assignmentTitle, DateTime? dueDate);
    Task SendWeeklyProgressReportAsync(string email, string name, WeeklyProgressData progress);

    // Payment failure notification
    Task SendPaymentFailedAsync(string email, string name, string tierName, decimal amount, string? failureReason);

    // Admin notifications
    Task SendAdminClassScheduledAsync(string studentName, string studentEmail, DateTime classDateTime);
    Task SendAdminClassCancelledAsync(string studentName, string studentEmail, DateTime classDateTime, string? reason);
    Task SendAdminClassReminderAsync(string studentName, string studentEmail, DateTime classDateTime, int hoursUntil);
    Task SendAdminPaymentReceivedAsync(string studentName, string studentEmail, decimal amount, string description, DateTime paymentDate);
    Task SendAdminContactInquiryAsync(string name, string email, string? phone, string message);
}
