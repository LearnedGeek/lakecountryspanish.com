using Microsoft.AspNetCore.Mvc;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Controllers;

public class ContactController : Controller
{
    private readonly ApplicationDbContext _context;

    public ContactController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new ContactViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ContactViewModel model)
    {

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

        // reCAPTCHA v3 is unreliable for mobile users (scores too low)
        // We keep it for monitoring but don't block based on score
        // The honeypot field above is our primary bot protection

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
}
