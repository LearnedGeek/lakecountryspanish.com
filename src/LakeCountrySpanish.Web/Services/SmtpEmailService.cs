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

    // Brand colors matching the site's Tailwind theme
    private const string PrimaryColor = "#4F46E5";      // Indigo - main brand color
    private const string PrimaryDark = "#3730A3";       // Darker indigo for headers
    private const string SecondaryColor = "#E85A42";    // Coral - CTA buttons
    private const string SuccessColor = "#059669";      // Green - confirmations
    private const string WarningColor = "#F59E0B";      // Amber - milestones/warnings
    private const string DangerColor = "#DC2626";       // Red - cancellations/errors
    private const string PurpleColor = "#8B5CF6";       // Purple - badges
    private const string SkyColor = "#0EA5E9";          // Sky blue - assignments

    public SmtpEmailService(
        IConfiguration configuration,
        ILogger<SmtpEmailService> logger,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    #region Template Helpers

    /// <summary>
    /// Creates a professionally branded email template with consistent styling
    /// </summary>
    private string BuildEmailTemplate(string headerTitle, string headerColor, string bodyContent, string? preheader = null, string? emoji = null)
    {
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";
        var logoUrl = $"{baseUrl}/img/lake-country-spanish-logo.png";
        var emojiSpan = !string.IsNullOrEmpty(emoji) ? $"<span style=\"font-size: 24px; margin-right: 8px;\">{emoji}</span>" : "";
        var preheaderHtml = !string.IsNullOrEmpty(preheader)
            ? $"<span style=\"display: none; max-height: 0; overflow: hidden; mso-hide: all;\">{preheader}</span>"
            : "";

        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
    <title>Lake Country Spanish</title>
    <!--[if mso]>
    <noscript>
        <xml>
            <o:OfficeDocumentSettings>
                <o:PixelsPerInch>96</o:PixelsPerInch>
            </o:OfficeDocumentSettings>
        </xml>
    </noscript>
    <![endif]-->
</head>
<body style=""margin: 0; padding: 0; background-color: #f3f4f6; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; -webkit-font-smoothing: antialiased;"">
    {preheaderHtml}

    <!-- Email wrapper -->
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #f3f4f6;"">
        <tr>
            <td align=""center"" style=""padding: 40px 20px;"">

                <!-- Main container -->
                <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width: 600px; width: 100%;"">

                    <!-- Logo header -->
                    <tr>
                        <td align=""center"" style=""padding-bottom: 24px;"">
                            <a href=""{baseUrl}"" style=""text-decoration: none;"">
                                <img src=""{logoUrl}"" alt=""Lake Country Spanish"" width=""70"" height=""70"" style=""border-radius: 50%; border: 3px solid #ffffff; box-shadow: 0 4px 12px rgba(0,0,0,0.15);"">
                            </a>
                        </td>
                    </tr>

                    <!-- Card -->
                    <tr>
                        <td>
                            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.07), 0 10px 20px rgba(0,0,0,0.04);"">

                                <!-- Header -->
                                <tr>
                                    <td style=""background: linear-gradient(135deg, {headerColor}, {AdjustColorBrightness(headerColor, -15)}); padding: 28px 40px; text-align: center;"">
                                        <h1 style=""margin: 0; color: #ffffff; font-size: 22px; font-weight: 700; letter-spacing: -0.3px;"">
                                            {emojiSpan}{headerTitle}
                                        </h1>
                                    </td>
                                </tr>

                                <!-- Body content -->
                                <tr>
                                    <td style=""padding: 36px 40px;"">
                                        {bodyContent}
                                    </td>
                                </tr>

                            </table>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""padding: 28px 20px; text-align: center;"">
                            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                                <tr>
                                    <td align=""center"" style=""padding-bottom: 16px;"">
                                        <a href=""{baseUrl}"" style=""color: {PrimaryColor}; text-decoration: none; font-weight: 600; font-size: 15px;"">Lake Country Spanish</a>
                                    </td>
                                </tr>
                                <tr>
                                    <td align=""center"" style=""padding-bottom: 16px;"">
                                        <span style=""color: #9ca3af; font-size: 13px;"">Personalized Online Spanish Lessons with Karen</span>
                                    </td>
                                </tr>
                                <tr>
                                    <td align=""center"">
                                        <a href=""{baseUrl}/Student/Dashboard"" style=""color: #6b7280; text-decoration: none; font-size: 12px;"">My Account</a>
                                        <span style=""color: #d1d5db; margin: 0 8px;"">•</span>
                                        <a href=""{baseUrl}/Contact"" style=""color: #6b7280; text-decoration: none; font-size: 12px;"">Contact</a>
                                        <span style=""color: #d1d5db; margin: 0 8px;"">•</span>
                                        <a href=""{baseUrl}"" style=""color: #6b7280; text-decoration: none; font-size: 12px;"">Website</a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    /// <summary>
    /// Creates a styled info box for highlighting important information
    /// </summary>
    private static string BuildInfoBox(string content, string borderColor, string? backgroundColor = null)
    {
        var bgColor = backgroundColor ?? "#f9fafb";
        return $@"
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin: 20px 0;"">
            <tr>
                <td style=""background-color: {bgColor}; border-left: 4px solid {borderColor}; border-radius: 8px; padding: 20px;"">
                    {content}
                </td>
            </tr>
        </table>";
    }

    /// <summary>
    /// Creates a styled card box for key information
    /// </summary>
    private static string BuildCardBox(string content, string? borderColor = null)
    {
        var border = borderColor != null ? $"border-left: 4px solid {borderColor};" : "border: 1px solid #e5e7eb;";
        return $@"
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin: 20px 0;"">
            <tr>
                <td style=""background-color: #ffffff; {border} border-radius: 12px; padding: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.08);"">
                    {content}
                </td>
            </tr>
        </table>";
    }

    /// <summary>
    /// Creates a styled call-to-action button
    /// </summary>
    private static string BuildButton(string text, string url, string backgroundColor)
    {
        return $@"
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin: 24px 0;"">
            <tr>
                <td align=""center"">
                    <!--[if mso]>
                    <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{url}"" style=""height:48px;v-text-anchor:middle;width:200px;"" arcsize=""17%"" strokecolor=""{backgroundColor}"" fillcolor=""{backgroundColor}"">
                    <w:anchorlock/>
                    <center style=""color:#ffffff;font-family:sans-serif;font-size:16px;font-weight:bold;"">{text}</center>
                    </v:roundrect>
                    <![endif]-->
                    <!--[if !mso]><!-->
                    <a href=""{url}"" style=""display: inline-block; background-color: {backgroundColor}; color: #ffffff; font-weight: 600; font-size: 15px; padding: 14px 32px; border-radius: 8px; text-decoration: none; box-shadow: 0 2px 4px rgba(0,0,0,0.12);"">{text}</a>
                    <!--<![endif]-->
                </td>
            </tr>
        </table>";
    }

    /// <summary>
    /// Creates a paragraph with consistent styling
    /// </summary>
    private static string BuildParagraph(string content)
    {
        return $@"<p style=""margin: 0 0 16px 0; color: #374151; font-size: 15px; line-height: 1.6;"">{content}</p>";
    }

    /// <summary>
    /// Creates a signature block
    /// </summary>
    private static string BuildSignature()
    {
        return @"
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin-top: 24px; border-top: 1px solid #e5e7eb; padding-top: 20px;"">
            <tr>
                <td>
                    <p style=""margin: 0; color: #374151; font-size: 15px; line-height: 1.6;"">
                        ¡Hasta pronto!<br>
                        <strong style=""color: #4F46E5;"">Karen</strong><br>
                        <span style=""color: #6b7280; font-size: 14px;"">Lake Country Spanish</span>
                    </p>
                </td>
            </tr>
        </table>";
    }

    /// <summary>
    /// Creates a date/time display box
    /// </summary>
    private static string BuildDateTimeBox(DateTime dateTime, string accentColor)
    {
        return $@"
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin: 8px 0;"">
            <tr>
                <td style=""background-color: #f9fafb; border-radius: 8px; padding: 16px; text-align: center;"">
                    <p style=""margin: 0 0 4px 0; font-size: 13px; color: #6b7280; text-transform: uppercase; letter-spacing: 0.5px;"">When</p>
                    <p style=""margin: 0; font-size: 18px; font-weight: 600; color: {accentColor};"">{dateTime:dddd, MMMM d, yyyy}</p>
                    <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: {accentColor};"">{dateTime:h:mm tt}</p>
                </td>
            </tr>
        </table>";
    }

    /// <summary>
    /// Adjusts color brightness for gradient effects
    /// </summary>
    private static string AdjustColorBrightness(string hexColor, int percent)
    {
        if (hexColor.StartsWith("#") && hexColor.Length == 7)
        {
            try
            {
                int r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(5, 2), 16);

                r = Math.Clamp(r + (r * percent / 100), 0, 255);
                g = Math.Clamp(g + (g * percent / 100), 0, 255);
                b = Math.Clamp(b + (b * percent / 100), 0, 255);

                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                return hexColor;
            }
        }
        return hexColor;
    }

    #endregion

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
                _logger.LogError("CRITICAL: Email not configured in production! Would have sent email to {ToEmail}: {Subject}", toEmail, subject);
            }
            else
            {
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
        }
    }

    public async Task SendClassScheduledAsync(string studentEmail, string studentName, DateTime classDateTime, string? classroomUrl)
    {
        var subject = "🎉 Your Spanish Class is Confirmed!";
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";

        var classroomSection = !string.IsNullOrEmpty(classroomUrl)
            ? $@"
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin: 16px 0;"">
                    <tr>
                        <td style=""background-color: #ecfdf5; border-radius: 8px; padding: 16px; text-align: center;"">
                            <p style=""margin: 0 0 8px 0; font-size: 14px; color: #059669; font-weight: 600;"">📹 Join your class here:</p>
                            <a href=""{classroomUrl}"" style=""color: #059669; text-decoration: underline; word-break: break-all;"">{classroomUrl}</a>
                        </td>
                    </tr>
                </table>"
            : "";

        var bodyContent = $@"
            {BuildParagraph($"Hi {studentName},")}
            {BuildParagraph("Great news! Your Spanish class has been scheduled and confirmed.")}
            {BuildDateTimeBox(classDateTime, SuccessColor)}
            {classroomSection}
            {BuildButton("View My Classes", $"{baseUrl}/Student/MyClasses", SecondaryColor)}
            {BuildParagraph("If you need to reschedule or cancel, please do so at least 24 hours in advance through your account.")}
            {BuildParagraph("¡Nos vemos pronto! (See you soon!)")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Class Confirmed!", SuccessColor, bodyContent,
            $"Your Spanish class is scheduled for {classDateTime:MMMM d} at {classDateTime:h:mm tt}", "🎉");

        await SendEmailAsync(studentEmail, studentName, subject, htmlBody);
    }

    public async Task SendClassRescheduledAsync(string studentEmail, string studentName, DateTime oldDateTime, DateTime newDateTime, string? reason)
    {
        var subject = "📅 Your Spanish Class Has Been Rescheduled";
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";

        var reasonSection = !string.IsNullOrEmpty(reason)
            ? $@"<p style=""margin: 16px 0 0 0; font-size: 14px; color: #6b7280;""><strong>Reason:</strong> {reason}</p>"
            : "";

        var timeChangeContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 12px 0;"">
                        <p style=""margin: 0; font-size: 13px; color: #6b7280; text-transform: uppercase;"">Original Time</p>
                        <p style=""margin: 4px 0 0 0; font-size: 16px; color: #9ca3af; text-decoration: line-through;"">{oldDateTime:dddd, MMMM d, yyyy} at {oldDateTime:h:mm tt}</p>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 12px 0;"">
                        <p style=""margin: 0; font-size: 13px; color: #059669; text-transform: uppercase; font-weight: 600;"">✓ New Time</p>
                        <p style=""margin: 4px 0 0 0; font-size: 18px; color: #059669; font-weight: 600;"">{newDateTime:dddd, MMMM d, yyyy} at {newDateTime:h:mm tt}</p>
                    </td>
                </tr>
            </table>
            {reasonSection}";

        var bodyContent = $@"
            {BuildParagraph($"Hi {studentName},")}
            {BuildParagraph("Your Spanish class has been rescheduled to a new time.")}
            {BuildCardBox(timeChangeContent, PrimaryColor)}
            {BuildParagraph("If this new time doesn't work for you, please log in to your account to reschedule or contact me directly.")}
            {BuildButton("View My Schedule", $"{baseUrl}/Student/MyClasses", PrimaryColor)}
            {BuildParagraph("Thank you for your flexibility!")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Class Rescheduled", PrimaryColor, bodyContent,
            $"Your class has been moved to {newDateTime:MMMM d} at {newDateTime:h:mm tt}", "📅");

        await SendEmailAsync(studentEmail, studentName, subject, htmlBody);
    }

    public async Task SendClassCancelledAsync(string studentEmail, string studentName, DateTime classDateTime, string? reason)
    {
        var subject = "Class Cancelled - Lake Country Spanish";
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";

        var reasonSection = !string.IsNullOrEmpty(reason)
            ? $@"<p style=""margin: 12px 0 0 0; font-size: 14px; color: #6b7280;""><strong>Reason:</strong> {reason}</p>"
            : "";

        var cancelledClassContent = $@"
            <p style=""margin: 0; font-size: 13px; color: #dc2626; text-transform: uppercase; font-weight: 600;"">Cancelled Class</p>
            <p style=""margin: 8px 0 0 0; font-size: 16px; color: #374151;"">{classDateTime:dddd, MMMM d, yyyy} at {classDateTime:h:mm tt}</p>
            {reasonSection}";

        var creditNoteContent = $@"
            <p style=""margin: 0; font-size: 15px; color: #059669;"">
                <strong>✓ Good news!</strong> Your class credit has been restored to your account. You can use it to schedule a new class at your convenience.
            </p>";

        var bodyContent = $@"
            {BuildParagraph($"Hi {studentName},")}
            {BuildParagraph("Unfortunately, your Spanish class has been cancelled.")}
            {BuildInfoBox(cancelledClassContent, DangerColor, "#fef2f2")}
            {BuildInfoBox(creditNoteContent, SuccessColor, "#ecfdf5")}
            {BuildButton("Schedule New Class", $"{baseUrl}/Student/MyClasses", SuccessColor)}
            {BuildParagraph("I apologize for any inconvenience. Please don't hesitate to reach out if you have any questions.")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Class Cancelled", DangerColor, bodyContent,
            $"Your class on {classDateTime:MMMM d} has been cancelled");

        await SendEmailAsync(studentEmail, studentName, subject, htmlBody);
    }

    public async Task SendClassReminderAsync(string email, string name, DateTime classDateTime, string? classroomUrl, int hoursUntil)
    {
        var subject = hoursUntil <= 1
            ? "⏰ Your Spanish Class Starts Soon!"
            : $"⏰ Reminder: Spanish Class in {hoursUntil} Hours";

        var joinSection = !string.IsNullOrEmpty(classroomUrl)
            ? BuildButton("Join Class Now", classroomUrl, SecondaryColor)
            : "";

        var bodyContent = $@"
            {BuildParagraph($"Hi {name},")}
            {BuildParagraph($"This is a friendly reminder about your upcoming Spanish class{(hoursUntil <= 1 ? " starting soon" : $" in {hoursUntil} hours")}!")}
            {BuildDateTimeBox(classDateTime, PrimaryColor)}
            {joinSection}
            {BuildParagraph("Make sure you're in a quiet space with good internet. ¡Nos vemos!")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Class Reminder", PrimaryColor, bodyContent,
            $"Your Spanish class starts {(hoursUntil <= 1 ? "soon" : $"in {hoursUntil} hours")}", "⏰");

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendPointMilestoneAsync(string email, string name, int totalPoints, int milestone, int tokensEarned)
    {
        var subject = $"🏆 Congratulations! You've Reached {milestone:N0} Points!";

        var tokenSection = tokensEarned > 0
            ? $@"
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin: 16px 0;"">
                    <tr>
                        <td style=""background-color: #ecfdf5; border-radius: 8px; padding: 16px; text-align: center;"">
                            <p style=""margin: 0; font-size: 15px; color: #059669;"">
                                <strong>🎁 Bonus Reward:</strong> You've earned <strong>{tokensEarned}</strong> free class token{(tokensEarned > 1 ? "s" : "")}!
                            </p>
                        </td>
                    </tr>
                </table>"
            : "";

        var milestoneContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""text-align: center; padding: 20px;"">
                        <p style=""margin: 0; font-size: 13px; color: #92400E; text-transform: uppercase; letter-spacing: 1px;"">Total Points</p>
                        <p style=""margin: 8px 0; font-size: 48px; font-weight: 700; color: {WarningColor};"">{totalPoints:N0}</p>
                        <p style=""margin: 0; font-size: 16px; color: #374151;"">You've reached the <strong>{milestone:N0} point</strong> milestone!</p>
                    </td>
                </tr>
            </table>";

        var bodyContent = $@"
            {BuildParagraph($"Hi {name},")}
            {BuildParagraph("Congratulations on reaching an amazing milestone in your Spanish learning journey!")}
            {BuildInfoBox(milestoneContent, WarningColor, "#fef3c7")}
            {tokenSection}
            {BuildParagraph("Keep up the excellent work! Every class, assignment, and activity brings you closer to Spanish fluency.")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Milestone Reached!", WarningColor, bodyContent,
            $"You've earned {totalPoints:N0} points!", "🏆");

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendBadgeEarnedAsync(string email, string name, string badgeName, string description, int bonusPoints)
    {
        var subject = $"🎖️ You've Earned a New Badge: {badgeName}!";

        var pointsSection = bonusPoints > 0
            ? $@"<p style=""margin: 16px 0 0 0; font-size: 15px; color: #7c3aed; font-weight: 600;"">+{bonusPoints} bonus points!</p>"
            : "";

        var badgeContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""text-align: center; padding: 20px;"">
                        <p style=""margin: 0; font-size: 24px; font-weight: 700; color: #7c3aed;"">{badgeName}</p>
                        <p style=""margin: 12px 0 0 0; font-size: 15px; color: #6b7280;"">{description}</p>
                        {pointsSection}
                    </td>
                </tr>
            </table>";

        var bodyContent = $@"
            {BuildParagraph($"Hi {name},")}
            {BuildParagraph("You've earned a new achievement badge!")}
            {BuildInfoBox(badgeContent, PurpleColor, "#f3e8ff")}
            {BuildParagraph("This badge has been added to your profile. Keep learning to unlock more achievements!")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("New Badge Earned!", PurpleColor, bodyContent,
            $"You've earned the {badgeName} badge!", "🎖️");

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendPaymentConfirmationAsync(string email, string name, decimal amount, string description, DateTime paymentDate)
    {
        var subject = "✓ Payment Confirmation - Lake Country Spanish";

        var receiptContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 12px 0; border-bottom: 1px solid #e5e7eb;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Description</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px;"">{description}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 12px 0; border-bottom: 1px solid #e5e7eb;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Date</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px;"">{paymentDate:MMMM d, yyyy}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 16px 0 0 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #374151; font-size: 15px; font-weight: 600;"">Amount Paid</td>
                                <td style=""text-align: right;"">
                                    <span style=""font-size: 28px; font-weight: 700; color: {SuccessColor};"">${amount:F2}</span>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>";

        var bodyContent = $@"
            {BuildParagraph($"Hi {name},")}
            {BuildParagraph("Thank you for your payment! Here's your receipt:")}
            {BuildCardBox(receiptContent)}
            {BuildParagraph("If you have any questions about this payment, please don't hesitate to contact me.")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Payment Confirmed", SuccessColor, bodyContent,
            $"Your payment of ${amount:F2} has been received", "✓");

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendSubscriptionRenewalAsync(string email, string name, string tierName, decimal amount, DateTime nextRenewalDate)
    {
        var subject = "✓ Subscription Renewed - Lake Country Spanish";

        var subscriptionContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Plan</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px; font-weight: 600;"">{tierName}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Amount</td>
                                <td style=""text-align: right; color: {PrimaryColor}; font-size: 14px; font-weight: 600;"">${amount:F2}/month</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Next Renewal</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px;"">{nextRenewalDate:MMMM d, yyyy}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>";

        var bodyContent = $@"
            {BuildParagraph($"Hi {name},")}
            {BuildParagraph("Your subscription has been renewed successfully!")}
            {BuildInfoBox(subscriptionContent, PrimaryColor, "#eef2ff")}
            {BuildParagraph("Your classes will continue as scheduled. If you need to make any changes to your subscription, you can do so from your account dashboard.")}
            {BuildParagraph("Thank you for continuing your Spanish learning journey with me!")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Subscription Renewed", PrimaryColor, bodyContent,
            $"Your {tierName} subscription has been renewed");

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendAssignmentAssignedAsync(string email, string name, string assignmentTitle, DateTime? dueDate)
    {
        var subject = "📝 New Assignment Available - Lake Country Spanish";
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";

        var dueDateSection = dueDate.HasValue
            ? $@"<p style=""margin: 12px 0 0 0; font-size: 14px; color: #6b7280;""><strong>Due:</strong> {dueDate.Value:MMMM d, yyyy}</p>"
            : "";

        var assignmentContent = $@"
            <p style=""margin: 0; font-size: 18px; font-weight: 600; color: #0369a1;"">{assignmentTitle}</p>
            {dueDateSection}";

        var bodyContent = $@"
            {BuildParagraph($"Hi {name},")}
            {BuildParagraph("You have a new assignment to complete!")}
            {BuildInfoBox(assignmentContent, SkyColor, "#f0f9ff")}
            {BuildButton("View Assignment", $"{baseUrl}/Student/Assignments", SkyColor)}
            {BuildParagraph("Log in to your account to start working on this assignment. ¡Buena suerte!")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("New Assignment", SkyColor, bodyContent,
            $"New assignment: {assignmentTitle}", "📝");

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendWeeklyProgressReportAsync(string email, string name, WeeklyProgressData progress)
    {
        var subject = "📊 Your Weekly Spanish Learning Report";

        var streakEmoji = progress.CurrentStreak >= 7 ? "🔥" : (progress.CurrentStreak >= 3 ? "⚡" : "");

        var statsContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td width=""50%"" style=""padding: 8px;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 8px;"">
                            <tr>
                                <td style=""padding: 16px; text-align: center;"">
                                    <p style=""margin: 0; font-size: 28px; font-weight: 700; color: {PrimaryColor};"">{progress.ClassesAttended}</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 11px; color: #6b7280; text-transform: uppercase;"">Classes</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                    <td width=""50%"" style=""padding: 8px;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 8px;"">
                            <tr>
                                <td style=""padding: 16px; text-align: center;"">
                                    <p style=""margin: 0; font-size: 28px; font-weight: 700; color: {PrimaryColor};"">{progress.AssignmentsCompleted}</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 11px; color: #6b7280; text-transform: uppercase;"">Assignments</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td width=""50%"" style=""padding: 8px;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 8px;"">
                            <tr>
                                <td style=""padding: 16px; text-align: center;"">
                                    <p style=""margin: 0; font-size: 28px; font-weight: 700; color: {WarningColor};"">{progress.PointsEarned}</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 11px; color: #6b7280; text-transform: uppercase;"">Points Earned</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                    <td width=""50%"" style=""padding: 8px;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 8px;"">
                            <tr>
                                <td style=""padding: 16px; text-align: center;"">
                                    <p style=""margin: 0; font-size: 28px; font-weight: 700; color: #ef4444;"">{progress.CurrentStreak} {streakEmoji}</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 11px; color: #6b7280; text-transform: uppercase;"">Day Streak</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>";

        var totalPointsContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""text-align: center; padding: 16px;"">
                        <p style=""margin: 0; font-size: 12px; color: #6b7280; text-transform: uppercase; letter-spacing: 1px;"">Total Points</p>
                        <p style=""margin: 8px 0 0 0; font-size: 36px; font-weight: 700; color: {PrimaryColor};"">{progress.TotalPoints:N0}</p>
                    </td>
                </tr>
            </table>";

        var milestoneSection = !string.IsNullOrEmpty(progress.NextMilestone) && progress.PointsToNextMilestone > 0
            ? $@"<p style=""margin: 16px 0; text-align: center; font-size: 14px; color: #6b7280;"">Only <strong>{progress.PointsToNextMilestone}</strong> points until: <strong>{progress.NextMilestone}</strong></p>"
            : "";

        var badgesSection = progress.BadgesEarned.Any()
            ? $@"<p style=""margin: 16px 0 0 0; font-size: 14px; color: #374151;""><strong>🎖️ Badges Earned:</strong> {string.Join(", ", progress.BadgesEarned)}</p>"
            : "";

        var bodyContent = $@"
            {BuildParagraph($"Hi {name},")}
            {BuildParagraph("Here's a summary of your Spanish learning progress this week!")}
            {statsContent}
            {BuildInfoBox(totalPointsContent, PrimaryColor, "#eef2ff")}
            {milestoneSection}
            {badgesSection}
            {BuildParagraph("Keep up the great work! Every little bit of practice helps you get closer to fluency.")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Weekly Progress Report", PrimaryColor, bodyContent,
            $"You earned {progress.PointsEarned} points this week!", "📊");

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    public async Task SendPaymentFailedAsync(string email, string name, string tierName, decimal amount, string? failureReason)
    {
        var subject = "⚠️ Action Required: Payment Failed";
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";

        var reasonSection = !string.IsNullOrEmpty(failureReason)
            ? $@"<p style=""margin: 12px 0 0 0; font-size: 14px; color: #6b7280;""><strong>Reason:</strong> {failureReason}</p>"
            : "";

        var alertContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 8px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Subscription</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px;"">{tierName}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 8px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Amount</td>
                                <td style=""text-align: right; color: #dc2626; font-size: 14px; font-weight: 600;"">${amount:F2}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
            {reasonSection}";

        var bodyContent = $@"
            {BuildParagraph($"Hi {name},")}
            {BuildParagraph("We were unable to process your subscription payment. Your subscription may be affected if this isn't resolved soon.")}
            {BuildInfoBox(alertContent, DangerColor, "#fef2f2")}
            {BuildParagraph("<strong>What to do:</strong> Please update your payment method or contact your card issuer to resolve this issue.")}
            {BuildButton("Update Payment Method", $"{baseUrl}/Student/Dashboard", PrimaryColor)}
            {BuildParagraph("If you need any assistance, please don't hesitate to contact me directly.")}
            {BuildSignature()}";

        var htmlBody = BuildEmailTemplate("Payment Failed", DangerColor, bodyContent,
            "Action required: Please update your payment method");

        await SendEmailAsync(email, name, subject, htmlBody);
    }

    #region Admin Notifications

    private string? GetAdminEmail() => _configuration["AppSettings:AdminEmail"];
    private string GetAdminName() => _configuration["AppSettings:AdminName"] ?? "Karen";

    public async Task SendAdminClassScheduledAsync(string studentName, string studentEmail, DateTime classDateTime)
    {
        var adminEmail = GetAdminEmail();
        if (string.IsNullOrEmpty(adminEmail))
        {
            _logger.LogDebug("Admin email not configured, skipping admin class scheduled notification");
            return;
        }

        var subject = $"📅 New Class Scheduled: {studentName}";

        var classContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Student</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px; font-weight: 600;"">{studentName}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Email</td>
                                <td style=""text-align: right; color: {PrimaryColor}; font-size: 14px;"">{studentEmail}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>";

        var bodyContent = $@"
            {BuildParagraph($"Hi {GetAdminName()},")}
            {BuildParagraph($"A new class has been scheduled with <strong>{studentName}</strong>.")}
            {BuildInfoBox(classContent, SuccessColor, "#ecfdf5")}
            {BuildDateTimeBox(classDateTime, SuccessColor)}
            {BuildParagraph("The student has been sent a confirmation email with the class details.")}";

        var htmlBody = BuildEmailTemplate("New Class Scheduled", SuccessColor, bodyContent,
            $"Class with {studentName} on {classDateTime:MMMM d} at {classDateTime:h:mm tt}", "📅");

        await SendEmailAsync(adminEmail, GetAdminName(), subject, htmlBody);
    }

    public async Task SendAdminClassCancelledAsync(string studentName, string studentEmail, DateTime classDateTime, string? reason)
    {
        var adminEmail = GetAdminEmail();
        if (string.IsNullOrEmpty(adminEmail))
        {
            _logger.LogDebug("Admin email not configured, skipping admin class cancelled notification");
            return;
        }

        var subject = $"❌ Class Cancelled: {studentName}";

        var reasonSection = !string.IsNullOrEmpty(reason)
            ? $@"<p style=""margin: 12px 0 0 0; font-size: 14px; color: #6b7280;""><strong>Reason:</strong> {reason}</p>"
            : "";

        var classContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Student</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px; font-weight: 600;"">{studentName}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Email</td>
                                <td style=""text-align: right; color: {PrimaryColor}; font-size: 14px;"">{studentEmail}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Original Time</td>
                                <td style=""text-align: right; color: #dc2626; font-size: 14px;"">{classDateTime:dddd, MMMM d, yyyy} at {classDateTime:h:mm tt}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
            {reasonSection}";

        var bodyContent = $@"
            {BuildParagraph($"Hi {GetAdminName()},")}
            {BuildParagraph($"<strong>{studentName}</strong> has cancelled their scheduled class.")}
            {BuildInfoBox(classContent, DangerColor, "#fef2f2")}
            {BuildParagraph("This time slot is now available for other students.")}";

        var htmlBody = BuildEmailTemplate("Class Cancelled", DangerColor, bodyContent,
            $"Class with {studentName} on {classDateTime:MMMM d} cancelled");

        await SendEmailAsync(adminEmail, GetAdminName(), subject, htmlBody);
    }

    public async Task SendAdminClassReminderAsync(string studentName, string studentEmail, DateTime classDateTime, int hoursUntil)
    {
        var adminEmail = GetAdminEmail();
        if (string.IsNullOrEmpty(adminEmail))
        {
            _logger.LogDebug("Admin email not configured, skipping admin class reminder notification");
            return;
        }

        var subject = hoursUntil <= 1
            ? $"⏰ Class Starting Soon: {studentName}"
            : $"⏰ Class in {hoursUntil} Hours: {studentName}";

        var classContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Student</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px; font-weight: 600;"">{studentName}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Email</td>
                                <td style=""text-align: right; color: {PrimaryColor}; font-size: 14px;"">{studentEmail}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>";

        var bodyContent = $@"
            {BuildParagraph($"Hi {GetAdminName()},")}
            {BuildParagraph($"Reminder: You have a class with <strong>{studentName}</strong> {(hoursUntil <= 1 ? "starting soon" : $"in {hoursUntil} hours")}.")}
            {BuildInfoBox(classContent, PrimaryColor, "#eef2ff")}
            {BuildDateTimeBox(classDateTime, PrimaryColor)}
            {BuildParagraph("The student has also been sent a reminder.")}";

        var htmlBody = BuildEmailTemplate("Upcoming Class Reminder", PrimaryColor, bodyContent,
            $"Class with {studentName} {(hoursUntil <= 1 ? "starting soon" : $"in {hoursUntil} hours")}", "⏰");

        await SendEmailAsync(adminEmail, GetAdminName(), subject, htmlBody);
    }

    public async Task SendAdminPaymentReceivedAsync(string studentName, string studentEmail, decimal amount, string description, DateTime paymentDate)
    {
        var adminEmail = GetAdminEmail();
        if (string.IsNullOrEmpty(adminEmail))
        {
            _logger.LogDebug("Admin email not configured, skipping admin payment notification");
            return;
        }

        var subject = $"💰 Payment Received: ${amount:F2} from {studentName}";

        var paymentContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 10px 0; border-bottom: 1px solid #e5e7eb;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Student</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px; font-weight: 600;"">{studentName}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0; border-bottom: 1px solid #e5e7eb;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Email</td>
                                <td style=""text-align: right; color: {PrimaryColor}; font-size: 14px;"">{studentEmail}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0; border-bottom: 1px solid #e5e7eb;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Description</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px;"">{description}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0; border-bottom: 1px solid #e5e7eb;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Date</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px;"">{paymentDate:MMMM d, yyyy}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 14px 0 0 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #374151; font-size: 15px; font-weight: 600;"">Amount Received</td>
                                <td style=""text-align: right;"">
                                    <span style=""font-size: 28px; font-weight: 700; color: {SuccessColor};"">${amount:F2}</span>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>";

        var bodyContent = $@"
            {BuildParagraph($"Hi {GetAdminName()},")}
            {BuildParagraph($"You've received a payment from <strong>{studentName}</strong>!")}
            {BuildCardBox(paymentContent)}
            {BuildParagraph("The student has been sent a confirmation receipt.")}";

        var htmlBody = BuildEmailTemplate("Payment Received", SuccessColor, bodyContent,
            $"${amount:F2} received from {studentName}", "💰");

        await SendEmailAsync(adminEmail, GetAdminName(), subject, htmlBody);
    }

    public async Task SendAdminContactInquiryAsync(string name, string email, string? phone, string message)
    {
        var adminEmail = GetAdminEmail();
        if (string.IsNullOrEmpty(adminEmail))
        {
            // Fall back to ContactForm:NotificationEmail for backwards compatibility
            adminEmail = _configuration["ContactForm:NotificationEmail"];
            if (string.IsNullOrEmpty(adminEmail))
            {
                _logger.LogWarning("No admin email configured for contact inquiry notification");
                return;
            }
        }

        var subject = $"📬 New Contact Inquiry from {name}";

        var phoneRow = !string.IsNullOrEmpty(phone)
            ? $@"
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Phone</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px;"">{phone}</td>
                            </tr>
                        </table>
                    </td>
                </tr>"
            : "";

        var contactContent = $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Name</td>
                                <td style=""text-align: right; color: #374151; font-size: 14px; font-weight: 600;"">{name}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 10px 0;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                            <tr>
                                <td style=""color: #6b7280; font-size: 14px;"">Email</td>
                                <td style=""text-align: right; color: {PrimaryColor}; font-size: 14px;"">{email}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                {phoneRow}
            </table>";

        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";

        var bodyContent = $@"
            {BuildParagraph($"Hi {GetAdminName()},")}
            {BuildParagraph("You've received a new inquiry from your website contact form!")}
            {BuildInfoBox(contactContent, PrimaryColor, "#eef2ff")}
            <div style=""margin: 20px 0;"">
                <p style=""margin: 0 0 8px 0; font-size: 13px; color: #6b7280; text-transform: uppercase; letter-spacing: 0.5px;"">Message</p>
                <div style=""background-color: #f9fafb; border-radius: 8px; padding: 16px; border: 1px solid #e5e7eb;"">
                    <p style=""margin: 0; color: #374151; font-size: 15px; line-height: 1.6; white-space: pre-wrap;"">{message}</p>
                </div>
            </div>
            {BuildButton("View in Dashboard", $"{baseUrl}/Admin/Inquiries", PrimaryColor)}";

        var htmlBody = BuildEmailTemplate("New Contact Inquiry", PrimaryColor, bodyContent,
            $"New message from {name}", "📬");

        await SendEmailAsync(adminEmail, GetAdminName(), subject, htmlBody);
    }

    #endregion
}
