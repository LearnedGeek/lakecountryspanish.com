using Microsoft.AspNetCore.Mvc;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace LakeCountrySpanish.Web.Controllers;

public class ContactController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmailService _emailService;
    private readonly ILogger<ContactController> _logger;

    public ContactController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IEmailService emailService,
        ILogger<ContactController> logger)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _emailService = emailService;
        _logger = logger;
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

        // Send notification email to admin using the template system
        // HTML encode user input to prevent XSS attacks in the message
        var encoder = HtmlEncoder.Default;
        var safeName = encoder.Encode(model.Name);
        var safeEmail = encoder.Encode(model.Email);
        var safePhone = !string.IsNullOrEmpty(model.Phone) ? encoder.Encode(model.Phone) : null;
        var safeMessage = encoder.Encode(model.Message);

        await _emailService.SendAdminContactInquiryAsync(safeName, safeEmail, safePhone, safeMessage);

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
        catch (Exception ex)
        {
            // Log the error but allow through to not block legitimate users
            _logger.LogWarning(ex, "reCAPTCHA verification failed - allowing request through");
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
