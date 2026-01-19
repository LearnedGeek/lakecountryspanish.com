using Microsoft.AspNetCore.Mvc;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Text.Json;

namespace LakeCountrySpanish.Web.Controllers;

public class ContactController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmailService _emailService;

    public ContactController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IEmailService emailService)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _emailService = emailService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.RecaptchaSiteKey = _configuration["ReCaptcha:SiteKey"];
        return View(new ContactViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ContactViewModel model)
    {
        ViewBag.RecaptchaSiteKey = _configuration["ReCaptcha:SiteKey"];

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Honeypot check - if filled, it's a bot
        if (!string.IsNullOrEmpty(model.Website))
        {
            // Silently reject - don't tell bots they were caught
            TempData["SuccessMessage"] = "Thank you for your message! Karen will get back to you soon.";
            return RedirectToAction(nameof(ThankYou));
        }

        // reCAPTCHA v3 verification - using very low threshold (0.05)
        // to only block obvious bots while allowing mobile users through
        var recaptchaResult = await VerifyRecaptchaAsync(model.RecaptchaToken);
        if (!recaptchaResult.Success || recaptchaResult.Score < 0.05m)
        {
            // Silently reject - don't tell bots they were caught
            TempData["SuccessMessage"] = "Thank you for your message! Karen will get back to you soon.";
            return RedirectToAction(nameof(ThankYou));
        }

        // Save inquiry
        var inquiry = new ContactInquiry
        {
            Name = model.Name,
            Email = model.Email,
            Phone = model.Phone,
            Message = model.Message,
            Status = InquiryStatus.New
        };

        _context.ContactInquiries.Add(inquiry);
        await _context.SaveChangesAsync();

        // Send notification email to Karen
        var adminEmail = _configuration["ContactForm:NotificationEmail"] ?? "karen@lakecountryspanish.com";
        var phoneInfo = !string.IsNullOrEmpty(model.Phone) ? $"<p><strong>Phone:</strong> {model.Phone}</p>" : "";
        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4F46E5; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 20px; border: 1px solid #e5e7eb; }}
        .info-box {{ background-color: #fff; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #4F46E5; }}
        .message-box {{ background-color: #fff; padding: 15px; border-radius: 8px; margin: 15px 0; border: 1px solid #e5e7eb; }}
        .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>New Contact Inquiry</h1>
        </div>
        <div class='content'>
            <p>You have received a new inquiry from the contact form on your website.</p>

            <div class='info-box'>
                <p><strong>Name:</strong> {model.Name}</p>
                <p><strong>Email:</strong> <a href='mailto:{model.Email}'>{model.Email}</a></p>
                {phoneInfo}
            </div>

            <div class='message-box'>
                <p><strong>Message:</strong></p>
                <p>{model.Message.Replace("\n", "<br/>")}</p>
            </div>

            <p>You can view and manage all inquiries in your <a href='https://lakecountryspanish.com/Admin/Inquiries'>admin dashboard</a>.</p>
        </div>
        <div class='footer'>
            <p>This is an automated notification from Lake Country Spanish.</p>
        </div>
    </div>
</body>
</html>";

        await _emailService.SendEmailAsync(adminEmail, "Karen", "New Contact Form Inquiry from " + model.Name, emailBody);

        TempData["SuccessMessage"] = "Thank you for your message! Karen will get back to you soon.";
        return RedirectToAction(nameof(ThankYou));
    }

    public IActionResult ThankYou()
    {
        return View();
    }

    private async Task<RecaptchaResponse> VerifyRecaptchaAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return new RecaptchaResponse { Success = false, Score = 0 };
        }

        var secretKey = _configuration["ReCaptcha:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
        {
            // If no secret key configured, allow through (dev environment)
            return new RecaptchaResponse { Success = true, Score = 1.0m };
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}",
                null);

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RecaptchaResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new RecaptchaResponse { Success = false, Score = 0 };
        }
        catch
        {
            // If verification fails, allow through to not block legitimate users
            return new RecaptchaResponse { Success = true, Score = 1.0m };
        }
    }
}

public class RecaptchaResponse
{
    public bool Success { get; set; }
    public decimal Score { get; set; }
    public string? Action { get; set; }
    public string? Hostname { get; set; }
}
