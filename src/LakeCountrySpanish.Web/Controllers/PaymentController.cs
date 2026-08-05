using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = AppRoles.Student)]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentService _paymentService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IConfiguration _configuration;

    public PaymentController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPaymentService paymentService,
        ISubscriptionService subscriptionService,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _paymentService = paymentService;
        _subscriptionService = subscriptionService;
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

    // Webhook endpoint for Stripe. Handles two families of events:
    //   * checkout.session.completed + charge.refunded  → StripePaymentService
    //   * customer.subscription.*  + invoice.paid|payment_succeeded|payment_failed
    //                              → StripeSubscriptionService
    // Both live behind this single URL so Karen only maintains one endpoint
    // in the Stripe dashboard (rather than adding a second subscription webhook,
    // which would need its own signing secret and dashboard config). Each service
    // still performs its own signature validation against Stripe:WebhookSecret.
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

        // Peek at the event type BEFORE signature verification so we can route
        // to the right service. This is safe: the destination service re-parses
        // and verifies the signature itself — the peek is used only for dispatch.
        var eventType = TryPeekEventType(json);

        if (IsSubscriptionEvent(eventType))
        {
            var ok = await _subscriptionService.ProcessSubscriptionWebhookAsync(json, signature);
            return ok ? Ok() : StatusCode(500, "Subscription webhook processing failed");
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

    private static string? TryPeekEventType(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsSubscriptionEvent(string? eventType) => eventType switch
    {
        "customer.subscription.created" => true,
        "customer.subscription.updated" => true,
        "customer.subscription.deleted" => true,
        "invoice.paid"                  => true,
        "invoice.payment_succeeded"     => true,
        "invoice.payment_failed"        => true,
        _ => false
    };

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
