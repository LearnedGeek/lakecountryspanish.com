using Microsoft.AspNetCore.Mvc;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using System.Net.Http;
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

        // Verify reCAPTCHA
        var recaptchaSecretKey = _configuration["ReCaptcha:SecretKey"];
        if (!string.IsNullOrEmpty(recaptchaSecretKey) && recaptchaSecretKey != "your_recaptcha_secret_key_here")
        {
            var recaptchaValid = await VerifyRecaptchaAsync(model.RecaptchaToken, recaptchaSecretKey);
            if (!recaptchaValid)
            {
                ModelState.AddModelError(string.Empty, "reCAPTCHA verification failed. Please try again.");
                return View(model);
            }
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

    private async Task<bool> VerifyRecaptchaAsync(string? token, string secretKey)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}",
                null);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RecaptchaResponse>(jsonResponse);

            // Lower threshold (0.3) to reduce false positives on mobile devices
            return result?.Success == true && result.Score >= 0.3;
        }
        catch
        {
            return false;
        }
    }

    private class RecaptchaResponse
    {
        public bool Success { get; set; }
        public double Score { get; set; }
        public string? Action { get; set; }
    }
}
