namespace LakeCountrySpanish.Web.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);
    Task SendClassRescheduledAsync(string studentEmail, string studentName, DateTime oldDateTime, DateTime newDateTime, string? reason);
    Task SendClassCancelledAsync(string studentEmail, string studentName, DateTime classDateTime, string? reason);
}
