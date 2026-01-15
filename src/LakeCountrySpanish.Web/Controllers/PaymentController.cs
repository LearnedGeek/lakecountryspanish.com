using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

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
    public async Task<IActionResult> Checkout(int? packageId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Only package purchases are supported - students must buy credits first
        if (!packageId.HasValue)
        {
            return RedirectToAction("PurchaseClasses", "Student");
        }

        var package = await _context.Packages.FindAsync(packageId.Value);
        if (package == null || !package.IsActive)
        {
            return NotFound();
        }

        var basePrice = await _paymentService.GetClassPriceForStudentAsync(user.Id);
        var savings = (basePrice * package.ClassCount) - package.Price;

        var viewModel = new CheckoutViewModel
        {
            PackageId = packageId,
            Amount = package.Price,
            Description = $"{package.Name} - {package.ClassCount} class credits",
            Package = package,
            Savings = savings
        };

        ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckoutSession(int packageId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var package = await _context.Packages.FindAsync(packageId);
        if (package == null || !package.IsActive)
        {
            return NotFound();
        }

        var successUrl = Url.Action("Success", "Payment", null, Request.Scheme);
        var cancelUrl = Url.Action("Cancel", "Payment", null, Request.Scheme);

        try
        {
            var checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(
                user.Id, packageId, null, package.Price, successUrl!, cancelUrl!);

            return Redirect(checkoutUrl);
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Unable to process payment. Please try again.";
            return RedirectToAction(nameof(Checkout), new { packageId });
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
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrEmpty(signature))
        {
            return BadRequest("Missing Stripe-Signature header");
        }

        var result = await _paymentService.ProcessWebhookAsync(json, signature);

        // Return appropriate status code based on result
        // Stripe will retry on 5xx errors, so we must be careful
        if (result.IsSignatureInvalid)
        {
            // 400 - Don't retry, signature is invalid
            return BadRequest("Invalid signature");
        }

        if (!result.Success && !result.AlreadyProcessed)
        {
            // 500 - Stripe should retry this webhook
            return StatusCode(500, result.ErrorMessage);
        }

        // 200 - Success or already processed (duplicate)
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
