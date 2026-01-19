using Microsoft.AspNetCore.Mvc;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using System.Text.Json;

namespace LakeCountrySpanish.Web.Controllers;

public class ContactController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public ContactController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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
