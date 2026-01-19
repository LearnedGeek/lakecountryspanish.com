using System.Net;
using System.Net.Mail;
using LakeCountrySpanish.Web.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;

namespace LakeCountrySpanish.Web.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly IWebHostEnvironment _environment;

    public SmtpEmailService(
        IConfiguration configuration,
        ILogger<SmtpEmailService> logger,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var smtpHost = _configuration["EmailSettings:SmtpHost"];
        var smtpPortStr = _configuration["EmailSettings:SmtpPort"];
        var smtpUser = _configuration["EmailSettings:SmtpUsername"];
        var smtpPass = _configuration["EmailSettings:SmtpPassword"];
        var fromEmail = _configuration["EmailSettings:FromEmail"];
        var fromName = _configuration["EmailSettings:FromName"] ?? "Lake Country Spanish";

        // Check if email is configured
        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(fromEmail))
        {
            if (_environment.IsProduction())
            {
                // In production, this is an error condition that needs attention
                _logger.LogError("CRITICAL: Email not configured in production! Would have sent email to {ToEmail}: {Subject}", toEmail, subject);
            }
            else
            {
                // In development, this is expected and just a warning
                _logger.LogWarning("Email not configured (dev environment). Would have sent email to {ToEmail}: {Subject}", toEmail, subject);
            }
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

    public async Task SendClassReminderAsync(string email, string name, DateTime classDateTime, string? classroomUrl, int hoursUntil)
    {
        var subject = hoursUntil <= 1
            ? "Your Spanish Class Starts Soon!"
            : $"Reminder: Spanish Class in {hoursUntil} Hours";

        var joinButton = !string.IsNullOrEmpty(classroomUrl)
            ? $@"<p style='text-align: center; margin: 20px 0;'>
                <a href='{classroomUrl}' style='background-color: #4F46E5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;'>Join Class</a>
               </p>"
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
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Class Reminder</h1>
        </div>
        <div class='content'>
            <p>Hi {name},</p>
            <p>This is a friendly reminder about your upcoming Spanish class!</p>

            <div class='highlight'>
                <p style='margin: 0;'><strong>When:</strong></p>
                <p style='margin: 5px 0; font-size: 18px; color: #4F46E5;'>{classDateTime:dddd, MMMM d, yyyy} at {classDateTime:h:mm tt}</p>
            </div>

            {joinButton}

            <p>See you soon!</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendPointMilestoneAsync(string email, string name, int totalPoints, int milestone, int tokensEarned)
    {
        var subject = $"Congratulations! You've Reached {milestone:N0} Points!";

        var tokenText = tokensEarned > 0
            ? $"<p><strong>Bonus Reward:</strong> You've earned <strong>{tokensEarned}</strong> free class token{(tokensEarned > 1 ? "s" : "")}!</p>"
            : "";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #F59E0B; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 20px; border: 1px solid #e5e7eb; }}
        .highlight {{ background-color: #FEF3C7; padding: 20px; border-radius: 8px; margin: 15px 0; text-align: center; }}
        .points {{ font-size: 48px; font-weight: bold; color: #F59E0B; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Milestone Reached!</h1>
        </div>
        <div class='content'>
            <p>Hi {name},</p>
            <p>Congratulations on reaching an amazing milestone in your Spanish learning journey!</p>

            <div class='highlight'>
                <p style='margin: 0; font-size: 14px; color: #92400E;'>TOTAL POINTS</p>
                <p class='points' style='margin: 10px 0;'>{totalPoints:N0}</p>
                <p style='margin: 0; font-size: 16px;'>You've reached the <strong>{milestone:N0} point</strong> milestone!</p>
            </div>

            {tokenText}

            <p>Keep up the excellent work! Every class, assignment, and activity brings you closer to Spanish fluency.</p>

            <p>Keep learning!</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendBadgeEarnedAsync(string email, string name, string badgeName, string description, int bonusPoints)
    {
        var subject = $"You've Earned a New Badge: {badgeName}!";

        var pointsText = bonusPoints > 0
            ? $"<p style='text-align: center; margin-top: 15px;'><strong>+{bonusPoints} bonus points!</strong></p>"
            : "";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #8B5CF6; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 20px; border: 1px solid #e5e7eb; }}
        .badge-box {{ background-color: #F3E8FF; padding: 20px; border-radius: 8px; margin: 15px 0; text-align: center; }}
        .badge-name {{ font-size: 24px; font-weight: bold; color: #7C3AED; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>New Badge Earned!</h1>
        </div>
        <div class='content'>
            <p>Hi {name},</p>
            <p>You've earned a new achievement badge!</p>

            <div class='badge-box'>
                <p class='badge-name' style='margin: 0;'>{badgeName}</p>
                <p style='margin: 10px 0 0 0; color: #6B7280;'>{description}</p>
                {pointsText}
            </div>

            <p>This badge has been added to your profile. Keep learning to unlock more achievements!</p>

            <p>Great job!</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendPaymentConfirmationAsync(string email, string name, decimal amount, string description, DateTime paymentDate)
    {
        var subject = "Payment Confirmation - Lake Country Spanish";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #059669; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 20px; border: 1px solid #e5e7eb; }}
        .receipt {{ background-color: #fff; padding: 20px; border-radius: 8px; margin: 15px 0; border: 1px solid #e5e7eb; }}
        .amount {{ font-size: 32px; font-weight: bold; color: #059669; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Payment Confirmed</h1>
        </div>
        <div class='content'>
            <p>Hi {name},</p>
            <p>Thank you for your payment! Here are the details:</p>

            <div class='receipt'>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 10px 0; border-bottom: 1px solid #e5e7eb;'><strong>Description</strong></td>
                        <td style='padding: 10px 0; border-bottom: 1px solid #e5e7eb; text-align: right;'>{description}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px 0; border-bottom: 1px solid #e5e7eb;'><strong>Date</strong></td>
                        <td style='padding: 10px 0; border-bottom: 1px solid #e5e7eb; text-align: right;'>{paymentDate:MMMM d, yyyy}</td>
                    </tr>
                    <tr>
                        <td style='padding: 15px 0;'><strong>Amount Paid</strong></td>
                        <td style='padding: 15px 0; text-align: right;'><span class='amount'>${amount:F2}</span></td>
                    </tr>
                </table>
            </div>

            <p>If you have any questions about this payment, please don't hesitate to contact me.</p>

            <p>Thank you!</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendSubscriptionRenewalAsync(string email, string name, string tierName, decimal amount, DateTime nextRenewalDate)
    {
        var subject = "Subscription Renewed - Lake Country Spanish";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4F46E5; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 20px; border: 1px solid #e5e7eb; }}
        .highlight {{ background-color: #EEF2FF; padding: 15px; border-radius: 8px; margin: 15px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Subscription Renewed</h1>
        </div>
        <div class='content'>
            <p>Hi {name},</p>
            <p>Your subscription has been renewed successfully!</p>

            <div class='highlight'>
                <table style='width: 100%;'>
                    <tr>
                        <td><strong>Plan:</strong></td>
                        <td style='text-align: right;'>{tierName}</td>
                    </tr>
                    <tr>
                        <td><strong>Amount:</strong></td>
                        <td style='text-align: right;'>${amount:F2}/month</td>
                    </tr>
                    <tr>
                        <td><strong>Next Renewal:</strong></td>
                        <td style='text-align: right;'>{nextRenewalDate:MMMM d, yyyy}</td>
                    </tr>
                </table>
            </div>

            <p>Your classes will continue as scheduled. If you need to make any changes to your subscription, you can do so from your account dashboard.</p>

            <p>Thank you for continuing your Spanish learning journey with me!</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendAssignmentAssignedAsync(string email, string name, string assignmentTitle, DateTime? dueDate)
    {
        var subject = "New Assignment Available - Lake Country Spanish";

        var dueDateText = dueDate.HasValue
            ? $"<p><strong>Due Date:</strong> {dueDate.Value:MMMM d, yyyy}</p>"
            : "";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0EA5E9; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 20px; border: 1px solid #e5e7eb; }}
        .assignment-box {{ background-color: #F0F9FF; padding: 20px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #0EA5E9; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>New Assignment</h1>
        </div>
        <div class='content'>
            <p>Hi {name},</p>
            <p>You have a new assignment to complete!</p>

            <div class='assignment-box'>
                <h3 style='margin: 0 0 10px 0; color: #0369A1;'>{assignmentTitle}</h3>
                {dueDateText}
            </div>

            <p style='text-align: center;'>
                <a href='#' style='background-color: #0EA5E9; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;'>View Assignment</a>
            </p>

            <p>Log in to your account to start working on this assignment. Good luck!</p>

            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendWeeklyProgressReportAsync(string email, string name, WeeklyProgressData progress)
    {
        var subject = "Your Weekly Spanish Learning Report";

        var badgesHtml = progress.BadgesEarned.Any()
            ? $"<p><strong>Badges Earned:</strong> {string.Join(", ", progress.BadgesEarned)}</p>"
            : "";

        var streakEmoji = progress.CurrentStreak >= 7 ? " " : (progress.CurrentStreak >= 3 ? " " : "");

        var milestoneText = !string.IsNullOrEmpty(progress.NextMilestone) && progress.PointsToNextMilestone > 0
            ? $"<p style='text-align: center; color: #6B7280;'>Only <strong>{progress.PointsToNextMilestone}</strong> points until your next milestone: <strong>{progress.NextMilestone}</strong></p>"
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
        .stats-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin: 20px 0; }}
        .stat-box {{ background-color: #fff; padding: 15px; border-radius: 8px; text-align: center; border: 1px solid #e5e7eb; }}
        .stat-value {{ font-size: 28px; font-weight: bold; color: #4F46E5; }}
        .stat-label {{ font-size: 12px; color: #6B7280; text-transform: uppercase; }}
        .total-points {{ background-color: #EEF2FF; padding: 20px; border-radius: 8px; text-align: center; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Weekly Progress Report</h1>
        </div>
        <div class='content'>
            <p>Hi {name},</p>
            <p>Here's a summary of your Spanish learning progress this week!</p>

            <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                <tr>
                    <td style='width: 50%; padding: 10px; text-align: center; background-color: #fff; border: 1px solid #e5e7eb; border-radius: 8px 0 0 0;'>
                        <div style='font-size: 28px; font-weight: bold; color: #4F46E5;'>{progress.ClassesAttended}</div>
                        <div style='font-size: 12px; color: #6B7280; text-transform: uppercase;'>Classes Attended</div>
                    </td>
                    <td style='width: 50%; padding: 10px; text-align: center; background-color: #fff; border: 1px solid #e5e7eb; border-radius: 0 8px 0 0;'>
                        <div style='font-size: 28px; font-weight: bold; color: #4F46E5;'>{progress.AssignmentsCompleted}</div>
                        <div style='font-size: 12px; color: #6B7280; text-transform: uppercase;'>Assignments Done</div>
                    </td>
                </tr>
                <tr>
                    <td style='width: 50%; padding: 10px; text-align: center; background-color: #fff; border: 1px solid #e5e7eb; border-radius: 0 0 0 8px;'>
                        <div style='font-size: 28px; font-weight: bold; color: #F59E0B;'>{progress.PointsEarned}</div>
                        <div style='font-size: 12px; color: #6B7280; text-transform: uppercase;'>Points Earned</div>
                    </td>
                    <td style='width: 50%; padding: 10px; text-align: center; background-color: #fff; border: 1px solid #e5e7eb; border-radius: 0 0 8px 0;'>
                        <div style='font-size: 28px; font-weight: bold; color: #EF4444;'>{progress.CurrentStreak}{streakEmoji}</div>
                        <div style='font-size: 12px; color: #6B7280; text-transform: uppercase;'>Day Streak</div>
                    </td>
                </tr>
            </table>

            <div class='total-points'>
                <p style='margin: 0; font-size: 14px; color: #6B7280;'>TOTAL POINTS</p>
                <p style='margin: 5px 0; font-size: 36px; font-weight: bold; color: #4F46E5;'>{progress.TotalPoints:N0}</p>
            </div>

            {milestoneText}
            {badgesHtml}

            <p>Keep up the great work! Every little bit of practice helps you get closer to fluency.</p>

            <p>See you in class!</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendPaymentFailedAsync(string email, string name, string tierName, decimal amount, string? failureReason)
    {
        var subject = "Action Required: Payment Failed - Lake Country Spanish";

        var reasonText = !string.IsNullOrEmpty(failureReason)
            ? $"<p><strong>Reason:</strong> {failureReason}</p>"
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
        .alert-box {{ background-color: #FEF2F2; padding: 20px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #DC2626; }}
        .action-box {{ background-color: #EEF2FF; padding: 20px; border-radius: 8px; margin: 15px 0; text-align: center; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Payment Failed</h1>
        </div>
        <div class='content'>
            <p>Hi {name},</p>
            <p>We were unable to process your subscription payment. Your subscription may be affected if this isn't resolved.</p>

            <div class='alert-box'>
                <table style='width: 100%;'>
                    <tr>
                        <td><strong>Subscription:</strong></td>
                        <td style='text-align: right;'>{tierName}</td>
                    </tr>
                    <tr>
                        <td><strong>Amount:</strong></td>
                        <td style='text-align: right;'>${amount:F2}</td>
                    </tr>
                </table>
                {reasonText}
            </div>

            <div class='action-box'>
                <p style='margin: 0 0 15px 0;'><strong>What to do:</strong></p>
                <p style='margin: 0;'>Please update your payment method or contact your card issuer to resolve this issue.</p>
                <p style='margin: 15px 0 0 0;'>
                    <a href='#' style='background-color: #4F46E5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;'>Update Payment Method</a>
                </p>
            </div>

            <p>If you need any assistance or have questions, please don't hesitate to contact me directly.</p>

            <p>Thank you,</p>
            <p>Karen<br/>Lake Country Spanish</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, name, subject, htmlBody);
    }
}
