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
public class SubscriptionController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IConfiguration _configuration;

    public SubscriptionController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
        _configuration = configuration;
    }

    /// <summary>
    /// Display available subscription plans.
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> Plans()
    {
        var tiers = await _subscriptionService.GetActiveSubscriptionTiersAsync();

        // If logged in, get current subscription
        Subscription? currentSubscription = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                currentSubscription = await _subscriptionService.GetActiveSubscriptionAsync(user.Id);
            }
        }

        var viewModel = new SubscriptionPlansViewModel
        {
            Tiers = tiers,
            CurrentSubscription = currentSubscription
        };

        return View(viewModel);
    }

    /// <summary>
    /// Start a subscription checkout session.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(int tierId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        // Check if user already has an active subscription
        var existingSubscription = await _subscriptionService.GetActiveSubscriptionAsync(user.Id);
        if (existingSubscription != null)
        {
            TempData["Error"] = "You already have an active subscription. Please manage your existing subscription or cancel it first.";
            return RedirectToAction(nameof(Manage));
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}/Subscription/Success";
        var cancelUrl = $"{baseUrl}/Subscription/Plans";

        try
        {
            var checkoutUrl = await _subscriptionService.CreateSubscriptionCheckoutAsync(
                user.Id,
                tierId,
                successUrl,
                cancelUrl);

            return Redirect(checkoutUrl);
        }
        catch
        {
            TempData["Error"] = "Unable to start subscription checkout. Please try again.";
            return RedirectToAction(nameof(Plans));
        }
    }

    /// <summary>
    /// Handle successful subscription checkout.
    /// </summary>
    public async Task<IActionResult> Success(string? session_id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        // The subscription will be created via webhook, just show success message
        TempData["Success"] = "Your subscription is being activated. You'll receive a confirmation email shortly.";
        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// Manage current subscription.
    /// </summary>
    public async Task<IActionResult> Manage()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(user.Id);
        var allSubscriptions = await _subscriptionService.GetStudentSubscriptionsAsync(user.Id);

        // Get recurring schedules if subscription exists
        IEnumerable<RecurringSchedule>? schedules = null;
        if (subscription != null)
        {
            schedules = await _subscriptionService.GetRecurringSchedulesAsync(subscription.Id);
        }

        // Get available time slots for scheduling
        var timeSlots = await _context.TimeSlots
            .Where(t => t.IsActive)
            .OrderBy(t => t.StartTime)
            .ToListAsync();

        var viewModel = new SubscriptionManageViewModel
        {
            CurrentSubscription = subscription,
            AllSubscriptions = allSubscriptions,
            RecurringSchedules = schedules ?? Enumerable.Empty<RecurringSchedule>(),
            AvailableTimeSlots = timeSlots,
            CanCancelThisMonth = subscription != null && _subscriptionService.CanCancelThisMonth(subscription),
            EffectiveCancellationDate = subscription != null ? _subscriptionService.GetEffectiveCancellationDate(subscription) : null
        };

        return View(viewModel);
    }

    /// <summary>
    /// Set recurring class schedule.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSchedule(int subscriptionId, List<RecurringScheduleInput> schedules)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var subscription = await _subscriptionService.GetSubscriptionAsync(subscriptionId);
        if (subscription == null || subscription.StudentId != user.Id)
        {
            TempData["Error"] = "Subscription not found.";
            return RedirectToAction(nameof(Manage));
        }

        var requests = schedules
            .Where(s => s.IsSelected)
            .Select(s => new RecurringScheduleRequest
            {
                TimeSlotId = s.TimeSlotId,
                DayOfWeek = s.DayOfWeek,
                Time = s.Time
            });

        var result = await _subscriptionService.SetRecurringScheduleAsync(subscriptionId, requests);

        if (result)
        {
            TempData["Success"] = "Your recurring schedule has been updated.";
        }
        else
        {
            TempData["Error"] = "Unable to update schedule. Please try again.";
        }

        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// Request subscription cancellation.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCancellation(int subscriptionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var result = await _subscriptionService.RequestCancellationAsync(subscriptionId, user.Id);

        if (result)
        {
            var subscription = await _subscriptionService.GetSubscriptionAsync(subscriptionId);
            var effectiveDate = subscription?.EffectiveCancellationDate?.ToString("MMMM d, yyyy") ?? "the end of your billing period";
            TempData["Success"] = $"Your cancellation request has been submitted. Your subscription will end on {effectiveDate}.";
        }
        else
        {
            TempData["Error"] = "Unable to process cancellation request. Please try again or contact support.";
        }

        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// Pause subscription.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pause(int subscriptionId, DateTime resumeDate)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var result = await _subscriptionService.PauseSubscriptionAsync(subscriptionId, user.Id, resumeDate);

        if (result)
        {
            TempData["Success"] = $"Your subscription has been paused until {resumeDate:MMMM d, yyyy}.";
        }
        else
        {
            TempData["Error"] = "Unable to pause subscription. Please try again.";
        }

        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// Resume paused subscription.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(int subscriptionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var result = await _subscriptionService.ResumeSubscriptionAsync(subscriptionId, user.Id);

        if (result)
        {
            TempData["Success"] = "Your subscription has been resumed.";
        }
        else
        {
            TempData["Error"] = "Unable to resume subscription. Please try again.";
        }

        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// Redirect to Stripe Customer Portal for billing management.
    /// </summary>
    public async Task<IActionResult> Portal()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var returnUrl = $"{baseUrl}/Subscription/Manage";

        var portalUrl = await _subscriptionService.GetCustomerPortalUrlAsync(user.Id, returnUrl);

        if (portalUrl == null)
        {
            TempData["Error"] = "Unable to access billing portal. You may not have an active subscription.";
            return RedirectToAction(nameof(Manage));
        }

        return Redirect(portalUrl);
    }

    /// <summary>
    /// API endpoint for processing subscription webhooks.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [Route("api/subscription/webhook")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrEmpty(signature))
        {
            return BadRequest("Missing Stripe-Signature header");
        }

        var result = await _subscriptionService.ProcessSubscriptionWebhookAsync(json, signature);

        // Return 500 on failure so Stripe will retry
        // Return 200 on success or if we want Stripe to stop retrying
        return result ? Ok() : StatusCode(500, "Webhook processing failed");
    }
}

/// <summary>
/// Input model for recurring schedule form.
/// </summary>
public class RecurringScheduleInput
{
    public bool IsSelected { get; set; }
    public int TimeSlotId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan Time { get; set; }
}
