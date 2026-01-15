using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);
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
}
