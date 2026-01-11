using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpanishScheduler.Web.Data;
using SpanishScheduler.Web.Models.Entities;
using SpanishScheduler.Web.Models.ViewModels;
using SpanishScheduler.Web.Services;

namespace SpanishScheduler.Web.Controllers;

[Authorize(Roles = "Student")]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;

    public PaymentController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPaymentService paymentService,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _paymentService = paymentService;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int? packageId, int? classId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var viewModel = new CheckoutViewModel();

        if (packageId.HasValue)
        {
            var package = await _context.Packages.FindAsync(packageId.Value);
            if (package == null || !package.IsActive)
            {
                return NotFound();
            }

            viewModel.PackageId = packageId;
            viewModel.Amount = package.Price;
            viewModel.Description = $"{package.Name} - {package.ClassCount} classes";
            viewModel.Package = package;
        }
        else if (classId.HasValue)
        {
            var scheduledClass = await _context.ScheduledClasses
                .Include(sc => sc.TimeSlot)
                .FirstOrDefaultAsync(sc => sc.Id == classId.Value && sc.StudentId == user.Id);

            if (scheduledClass == null)
            {
                return NotFound();
            }

            viewModel.ClassId = classId;
            viewModel.Amount = await _paymentService.GetClassPriceForStudentAsync(user.Id);
            viewModel.Description = $"Spanish Lesson - {scheduledClass.ClassDateTime:MMMM d, yyyy 'at' h:mm tt}";
            viewModel.ScheduledClass = scheduledClass;
        }
        else
        {
            return RedirectToAction("Dashboard", "Student");
        }

        ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckoutSession(int? packageId, int? classId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        decimal amount;

        if (packageId.HasValue)
        {
            var package = await _context.Packages.FindAsync(packageId.Value);
            if (package == null || !package.IsActive)
            {
                return NotFound();
            }
            amount = package.Price;
        }
        else if (classId.HasValue)
        {
            amount = await _paymentService.GetClassPriceForStudentAsync(user.Id);
        }
        else
        {
            return BadRequest();
        }

        var successUrl = Url.Action("Success", "Payment", null, Request.Scheme);
        var cancelUrl = Url.Action("Cancel", "Payment", null, Request.Scheme);

        try
        {
            var checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(
                user.Id, packageId, classId, amount, successUrl!, cancelUrl!);

            return Redirect(checkoutUrl);
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Unable to process payment. Please try again.";
            return RedirectToAction(nameof(Checkout), new { packageId, classId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Success(string session_id)
    {
        if (string.IsNullOrEmpty(session_id))
        {
            return RedirectToAction("Dashboard", "Student");
        }

        var payment = await _paymentService.GetPaymentBySessionIdAsync(session_id);

        if (payment != null && payment.Status == PaymentStatusType.Completed)
        {
            TempData["SuccessMessage"] = "Payment successful! Thank you for your purchase.";
        }
        else
        {
            // Payment may still be processing via webhook
            TempData["SuccessMessage"] = "Payment received! Your account will be updated shortly.";
        }

        return RedirectToAction("Dashboard", "Student");
    }

    [HttpGet]
    public IActionResult Cancel()
    {
        TempData["ErrorMessage"] = "Payment was cancelled.";
        return RedirectToAction("Dashboard", "Student");
    }

    // Webhook endpoint for Stripe
    [HttpPost]
    [AllowAnonymous]
    [Route("api/payment/webhook")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"];

        if (string.IsNullOrEmpty(signature))
        {
            return BadRequest();
        }

        var payment = await _paymentService.ProcessWebhookAsync(json, signature!);

        return Ok();
    }

    // Buy Package
    [HttpGet]
    public async Task<IActionResult> BuyPackage(int id)
    {
        var package = await _context.Packages.FindAsync(id);
        if (package == null || !package.IsActive)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Checkout), new { packageId = id });
    }
}
