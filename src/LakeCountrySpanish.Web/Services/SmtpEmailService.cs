using System.Net;
using System.Net.Mail;

namespace LakeCountrySpanish.Web.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPortStr = _configuration["Email:SmtpPort"];
        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPass = _configuration["Email:SmtpPassword"];
        var fromEmail = _configuration["Email:FromEmail"];
        var fromName = _configuration["Email:FromName"] ?? "Lake Country Spanish";

        // Check if email is configured
        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(fromEmail))
        {
            _logger.LogWarning("Email not configured. Would have sent email to {ToEmail}: {Subject}", toEmail, subject);
            return;
        }

        try
        {
            var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = !string.IsNullOrEmpty(smtpUser)
                    ? new NetworkCredential(smtpUser, smtpPass)
                    : null
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent successfully to {ToEmail}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}: {Subject}", toEmail, subject);
            // Don't throw - email failures shouldn't break the main flow
        }
    }

    public async Task SendClassRescheduledAsync(string studentEmail, string studentName, DateTime oldDateTime, DateTime newDateTime, string? reason)
    {
        var subject = "Your Spanish Class Has Been Rescheduled";

        var reasonText = !string.IsNullOrEmpty(reason)
            ? $"<p><strong>Reason:</strong> {reason}</p>"
            : "";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4F46E5; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 20px; border: 1px solid #e5e7eb; }}
        .highlight {{ background-color: #fff; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #4F46E5; }}
        .old-time {{ color: #6b7280; text-decoration: line-through; }}
        .new-time {{ color: #059669; font-weight: bold; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Class Rescheduled</h1>
        </div>
        <div class='content'>
            <p>Hi {studentName},</p>
            <p>Your Spanish class has been rescheduled to a new time.</p>

            <div class='highlight'>
                <p style='margin: 0;'><strong>Original Time:</strong></p>
                <p class='old-time' style='margin: 5px 0;'>{oldDateTime:dddd, MMMM d, yyyy} at {oldDateTime:h:mm tt}</p>

                <p style='margin: 15px 0 0 0;'><strong>New Time:</strong></p>
                <p class='new-time' style='margin: 5px 0;'>{newDateTime:dddd, MMMM d, yyyy} at {newDateTime:h:mm tt}</p>
            </div>

            {reasonText}

            <p>If this new time doesn't work for you, please log in to your account to reschedule or contact me directly.</p>

            <p>Thank you for your understanding!</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(studentEmail, studentName, subject, htmlBody);
    }

    public async Task SendClassCancelledAsync(string studentEmail, string studentName, DateTime classDateTime, string? reason)
    {
        var subject = "Your Spanish Class Has Been Cancelled";

        var reasonText = !string.IsNullOrEmpty(reason)
            ? $"<p><strong>Reason:</strong> {reason}</p>"
            : "";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #DC2626; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 20px; border: 1px solid #e5e7eb; }}
        .highlight {{ background-color: #fff; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #DC2626; }}
        .credit-note {{ background-color: #ECFDF5; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #059669; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Class Cancelled</h1>
        </div>
        <div class='content'>
            <p>Hi {studentName},</p>
            <p>Unfortunately, your Spanish class has been cancelled.</p>

            <div class='highlight'>
                <p style='margin: 0;'><strong>Cancelled Class:</strong></p>
                <p style='margin: 5px 0;'>{classDateTime:dddd, MMMM d, yyyy} at {classDateTime:h:mm tt}</p>
            </div>

            {reasonText}

            <div class='credit-note'>
                <p style='margin: 0;'><strong>Good news!</strong> Your class credit has been restored to your account. You can use it to schedule a new class at your convenience.</p>
            </div>

            <p>I apologize for any inconvenience. Please log in to your account to schedule a new class, or contact me if you have any questions.</p>

            <p>Thank you for your understanding!</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(studentEmail, studentName, subject, htmlBody);
    }
}
