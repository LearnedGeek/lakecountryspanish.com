using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services.Programs;
// Aliases to resolve ambiguity with Stripe types
using StripeSubscription = Stripe.Subscription;
using StripeSubService = Stripe.SubscriptionService;
using Subscription = LakeCountrySpanish.Web.Models.Entities.Subscription;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Stripe-backed implementation of subscription management.
/// </summary>
public class StripeSubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeSubscriptionService> _logger;
    private readonly ITicketService _ticketService;
    private readonly IEmailService _emailService;
    private readonly IProgramEnrollmentService _enrollmentService;

    public StripeSubscriptionService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<StripeSubscriptionService> logger,
        ITicketService ticketService,
        IEmailService emailService,
        IProgramEnrollmentService enrollmentService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _ticketService = ticketService;
        _emailService = emailService;
        _enrollmentService = enrollmentService;
    }

    #region Tier Queries

    public async Task<IEnumerable<SubscriptionTier>> GetActiveSubscriptionTiersAsync()
    {
        return await _context.SubscriptionTiers
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();
    }

    public async Task<SubscriptionTier?> GetSubscriptionTierAsync(int tierId)
    {
        return await _context.SubscriptionTiers.FindAsync(tierId);
    }

    #endregion

    #region Subscription Queries

    public async Task<Subscription?> GetActiveSubscriptionAsync(string studentId)
    {
        return await _context.Subscriptions
            .Include(s => s.Tier)
            .Include(s => s.RecurringSchedules)
            .FirstOrDefaultAsync(s =>
                s.StudentId == studentId &&
                s.Status == SubscriptionStatus.Active);
    }

    public async Task<Subscription?> GetSubscriptionAsync(int subscriptionId)
    {
        return await _context.Subscriptions
            .Include(s => s.Tier)
            .Include(s => s.RecurringSchedules)
            .Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);
    }

    public async Task<IEnumerable<Subscription>> GetStudentSubscriptionsAsync(string studentId)
    {
        return await _context.Subscriptions
            .Include(s => s.Tier)
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    #endregion

    #region Subscription Creation

    public async Task<string> CreateSubscriptionCheckoutAsync(
        string studentId,
        int tierId,
        string successUrl,
        string cancelUrl)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
            throw new ArgumentException("Student not found", nameof(studentId));

        var tier = await GetSubscriptionTierAsync(tierId);
        if (tier == null || string.IsNullOrEmpty(tier.StripePriceId))
            throw new ArgumentException("Invalid subscription tier", nameof(tierId));

        // Ensure student has a Stripe Customer ID
        var customerId = await GetOrCreateStripeCustomerAsync(studentId);

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = tier.StripePriceId,
                    Quantity = 1
                }
            },
            Mode = "subscription",
            SuccessUrl = $"{successUrl}?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                { "studentId", studentId },
                { "tierId", tierId.ToString() }
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "studentId", studentId },
                    { "tierId", tierId.ToString() }
                }
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return session.Url;
    }

    public async Task<bool> ProcessSubscriptionWebhookAsync(string json, string signature)
    {
        var webhookSecret = _configuration["Stripe:SubscriptionWebhookSecret"]
            ?? _configuration["Stripe:WebhookSecret"];

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
            _logger.LogInformation("Processing subscription webhook: {EventType}", stripeEvent.Type);

            switch (stripeEvent.Type)
            {
                case "customer.subscription.created":
                    await HandleSubscriptionCreated(stripeEvent);
                    break;

                case "customer.subscription.updated":
                    await HandleSubscriptionUpdated(stripeEvent);
                    break;

                case "customer.subscription.deleted":
                    await HandleSubscriptionDeleted(stripeEvent);
                    break;

                // Modern Stripe API versions emit "invoice.paid" for successful
                // invoice payments; older versions emitted "invoice.payment_succeeded".
                // Both carry the same Invoice payload — accept both aliases so the
                // handler works regardless of API version negotiation.
                case "invoice.paid":
                case "invoice.payment_succeeded":
                    await HandleInvoicePaymentSucceeded(stripeEvent);
                    break;

                case "invoice.payment_failed":
                    await HandleInvoicePaymentFailed(stripeEvent);
                    break;

                default:
                    _logger.LogInformation("Unhandled subscription event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return true;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook error");
            return false;
        }
    }

    private async Task HandleSubscriptionCreated(Event stripeEvent)
    {
        var stripeSubscription = stripeEvent.Data.Object as StripeSubscription;
        if (stripeSubscription == null) return;

        var studentId = stripeSubscription.Metadata.GetValueOrDefault("studentId");
        var tierIdStr = stripeSubscription.Metadata.GetValueOrDefault("tierId");

        if (string.IsNullOrEmpty(studentId) || !int.TryParse(tierIdStr, out var tierId))
        {
            _logger.LogWarning("Subscription created without valid metadata");
            return;
        }

        // Check if subscription already exists
        var existing = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (existing != null)
        {
            _logger.LogInformation("Subscription {StripeId} already exists", stripeSubscription.Id);
            return;
        }

        // In Stripe.net v48+, period dates moved to SubscriptionItem
        var firstItem = stripeSubscription.Items?.Data?.FirstOrDefault();

        var subscription = new Subscription
        {
            StudentId = studentId,
            SubscriptionTierId = tierId,
            StripeSubscriptionId = stripeSubscription.Id,
            StripeCustomerId = stripeSubscription.CustomerId,
            Status = MapStripeStatus(stripeSubscription.Status),
            StartDate = stripeSubscription.StartDate,
            CurrentPeriodStart = firstItem?.CurrentPeriodStart,
            CurrentPeriodEnd = firstItem?.CurrentPeriodEnd
        };

        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Add history entry
        await AddHistoryEntry(subscription.Id, SubscriptionEvent.Created,
            $"Subscription created via Stripe. Tier: {tierId}");

        _logger.LogInformation("Created subscription {Id} for student {StudentId}",
            subscription.Id, studentId);
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        var stripeSubscription = stripeEvent.Data.Object as StripeSubscription;
        if (stripeSubscription == null) return;

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (subscription == null)
        {
            _logger.LogWarning("Subscription {StripeId} not found for update", stripeSubscription.Id);
            return;
        }

        var previousStatus = subscription.Status;
        subscription.Status = MapStripeStatus(stripeSubscription.Status);

        // In Stripe.net v48+, period dates moved to SubscriptionItem
        var firstItem = stripeSubscription.Items?.Data?.FirstOrDefault();
        if (firstItem?.CurrentPeriodStart != null)
            subscription.CurrentPeriodStart = firstItem.CurrentPeriodStart;
        if (firstItem?.CurrentPeriodEnd != null)
            subscription.CurrentPeriodEnd = firstItem.CurrentPeriodEnd;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (stripeSubscription.CanceledAt.HasValue)
        {
            subscription.CancelledAt = stripeSubscription.CanceledAt.Value;
        }

        await _context.SaveChangesAsync();

        if (previousStatus != subscription.Status)
        {
            await AddHistoryEntry(subscription.Id, SubscriptionEvent.Renewed,
                $"Status changed from {previousStatus} to {subscription.Status}");
        }
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var stripeSubscription = stripeEvent.Data.Object as StripeSubscription;
        if (stripeSubscription == null) return;

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (subscription == null) return;

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await AddHistoryEntry(subscription.Id, SubscriptionEvent.Cancelled, "Subscription deleted in Stripe");
    }

    private async Task HandleInvoicePaymentSucceeded(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;

        // First look: does this invoice belong to a program-enrollment installment
        // subscription? If yes, the enrollment service owns the state transition
        // and we skip the LCS Subscription tier flow entirely.
        if (await _enrollmentService.HandleInvoicePaidAsync(invoice))
        {
            return;
        }

        // Get subscription ID from invoice - try multiple approaches for SDK compatibility
        var stripeSubscriptionId = GetSubscriptionIdFromInvoice(invoice);
        if (string.IsNullOrEmpty(stripeSubscriptionId))
        {
            // Try to find subscription by customer
            var customerId = invoice.CustomerId;
            if (string.IsNullOrEmpty(customerId)) return;

            var subscription = await _context.Subscriptions
                .Include(s => s.Tier)
                .FirstOrDefaultAsync(s => s.StripeCustomerId == customerId && s.Status == SubscriptionStatus.Active);

            if (subscription == null) return;

            await ProcessSuccessfulPayment(subscription, invoice);
            return;
        }

        var sub = await _context.Subscriptions
            .Include(s => s.Tier)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);

        if (sub == null) return;

        await ProcessSuccessfulPayment(sub, invoice);
    }

    private async Task ProcessSuccessfulPayment(Subscription subscription, Invoice invoice)
    {
        // Reset monthly class counter on successful renewal
        subscription.ClassesUsedThisMonth = 0;
        subscription.Status = SubscriptionStatus.Active;
        subscription.UpdatedAt = DateTime.UtcNow;

        // Get payment intent ID from invoice
        var paymentIntentId = GetPaymentIntentIdFromInvoice(invoice);

        // Create payment record
        var payment = new Payment
        {
            StudentId = subscription.StudentId,
            Amount = invoice.AmountPaid / 100m, // Stripe uses cents
            StripePaymentIntentId = paymentIntentId,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.Subscription,
            SubscriptionId = subscription.Id,
            CompletedAt = DateTime.UtcNow,
            Description = $"Subscription renewal - {subscription.Tier?.Name ?? "Unknown tier"}"
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        await AddHistoryEntry(subscription.Id, SubscriptionEvent.PaymentSucceeded,
            $"Payment of ${payment.Amount:F2} succeeded");
        await AddHistoryEntry(subscription.Id, SubscriptionEvent.ClassesReset,
            "Monthly class counter reset");

        // Grant tickets for this subscription period
        await GrantSubscriptionTicketsAsync(subscription);

        // Generate classes for new period (for recurring schedules)
        await GenerateClassesForSubscriptionAsync(subscription.Id);

        // Send subscription renewal email notification
        try
        {
            var student = await _context.Users.FindAsync(subscription.StudentId);
            if (student != null && subscription.Tier != null)
            {
                await _emailService.SendSubscriptionRenewalAsync(
                    student.Email!,
                    student.FirstName ?? student.Email!,
                    subscription.Tier.Name,
                    payment.Amount,
                    subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send subscription renewal email for Subscription {SubscriptionId}", subscription.Id);
        }
    }

    /// <summary>
    /// Grant tickets to the student based on their subscription tier.
    /// Tickets expire at the end of the subscription period.
    /// </summary>
    private async Task GrantSubscriptionTicketsAsync(Subscription subscription)
    {
        if (subscription.Tier == null)
        {
            _logger.LogWarning("Subscription {SubscriptionId} has no tier, cannot grant tickets", subscription.Id);
            return;
        }

        var ticketCount = subscription.Tier.ClassesPerMonth;
        if (ticketCount <= 0)
        {
            _logger.LogInformation("Subscription tier {TierName} has no classes, skipping ticket grant", subscription.Tier.Name);
            return;
        }

        // Tickets expire at the end of the current subscription period
        var expiresAt = subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);

        var tickets = await _ticketService.GrantSubscriptionTicketsAsync(
            subscription.StudentId,
            subscription.Id,
            ticketCount,
            expiresAt);

        _logger.LogInformation(
            "Granted {TicketCount} subscription tickets to student {StudentId} for subscription {SubscriptionId}",
            tickets.Count(), subscription.StudentId, subscription.Id);

        await AddHistoryEntry(subscription.Id, SubscriptionEvent.ClassesReset,
            $"Granted {ticketCount} class tickets for this period");
    }

    /// <summary>
    /// Gets subscription ID from invoice using available properties for SDK compatibility.
    /// </summary>
    private string? GetSubscriptionIdFromInvoice(Invoice invoice)
    {
        // Try to get subscription from invoice lines (works across SDK versions)
        // In Stripe.net v50+, Subscription is an object, access the Id property
        var subscriptionLine = invoice.Lines?.Data?
            .FirstOrDefault(l => l.Subscription?.Id != null);
        if (subscriptionLine?.Subscription?.Id != null)
            return subscriptionLine.Subscription.Id;

        // Try parent subscription details (newer API versions)
        try
        {
            var parent = invoice.Parent as InvoiceParent;
            if (parent?.SubscriptionDetails?.Subscription?.Id != null)
                return parent.SubscriptionDetails.Subscription.Id;
        }
        catch
        {
            // Property may not exist in this SDK version
        }

        return null;
    }

    /// <summary>
    /// Gets payment intent ID from invoice.
    /// Note: In Stripe.net v50+ webhook events, the PaymentIntent may not be directly accessible.
    /// This is optional for tracking purposes only.
    /// </summary>
    private string? GetPaymentIntentIdFromInvoice(Invoice invoice)
    {
        // In Stripe API v2025+, the PaymentIntent is not a direct property on Invoice objects
        // in webhook events. If needed for detailed tracking, use the Stripe Dashboard or
        // fetch the invoice with expanded PaymentIntent. For our purposes, we'll store null
        // and the payment can be tracked via the subscription relationship.
        return null;
    }

    private async Task HandleInvoicePaymentFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;

        // Get subscription ID from invoice
        var stripeSubscriptionId = GetSubscriptionIdFromInvoice(invoice);
        Subscription? subscription = null;

        if (!string.IsNullOrEmpty(stripeSubscriptionId))
        {
            subscription = await _context.Subscriptions
                .Include(s => s.Student)
                .Include(s => s.Tier)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);
        }
        else
        {
            // Try to find subscription by customer
            var customerId = invoice.CustomerId;
            if (!string.IsNullOrEmpty(customerId))
            {
                subscription = await _context.Subscriptions
                    .Include(s => s.Student)
                    .Include(s => s.Tier)
                    .FirstOrDefaultAsync(s => s.StripeCustomerId == customerId && s.Status == SubscriptionStatus.Active);
            }
        }

        if (subscription == null)
        {
            _logger.LogWarning("Could not find subscription for failed payment invoice {InvoiceId}", invoice.Id);
            return;
        }

        subscription.Status = SubscriptionStatus.PastDue;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await AddHistoryEntry(subscription.Id, SubscriptionEvent.PaymentFailed,
            $"Payment failed for invoice {invoice.Id}");

        // Send notification email to student
        try
        {
            if (subscription.Student != null && !string.IsNullOrEmpty(subscription.Student.Email))
            {
                var amount = invoice.AmountDue / 100m; // Stripe uses cents
                var tierName = subscription.Tier?.Name ?? "Subscription";

                // Note: In Stripe.net v50+, the Charge object is not directly accessible from Invoice
                // in webhook events. The failure reason would need to be fetched separately if needed.
                // For now, we send the notification without a specific failure reason.
                string? failureReason = null;

                await _emailService.SendPaymentFailedAsync(
                    subscription.Student.Email,
                    subscription.Student.FullName ?? subscription.Student.Email,
                    tierName,
                    amount,
                    failureReason);

                _logger.LogInformation("Sent payment failure notification to student {StudentId} for subscription {SubscriptionId}",
                    subscription.StudentId, subscription.Id);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the webhook processing if email fails
            _logger.LogError(ex, "Failed to send payment failure notification email for subscription {SubscriptionId}",
                subscription.Id);
        }
    }

    private SubscriptionStatus MapStripeStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Cancelled,
            "paused" => SubscriptionStatus.Paused,
            "unpaid" => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.Active
        };
    }

    #endregion

    #region Subscription Management

    public async Task<bool> RequestCancellationAsync(int subscriptionId, string studentId)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId);
        if (subscription == null || subscription.StudentId != studentId)
            return false;

        subscription.CancellationRequested = true;
        subscription.CancellationRequestDate = DateTime.UtcNow;
        subscription.EffectiveCancellationDate = GetEffectiveCancellationDate(subscription);
        subscription.UpdatedAt = DateTime.UtcNow;

        // Schedule cancellation in Stripe at period end
        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            var service = new StripeSubService();
            await service.UpdateAsync(subscription.StripeSubscriptionId,
                new Stripe.SubscriptionUpdateOptions { CancelAtPeriodEnd = true });
        }

        await _context.SaveChangesAsync();
        await AddHistoryEntry(subscriptionId, SubscriptionEvent.CancellationRequested,
            $"Cancellation requested. Effective: {subscription.EffectiveCancellationDate:d}");

        return true;
    }

    public async Task<bool> CancelSubscriptionAsync(int subscriptionId, string studentId)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId);
        if (subscription == null || subscription.StudentId != studentId)
            return false;

        // Cancel immediately in Stripe
        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            var service = new StripeSubService();
            await service.CancelAsync(subscription.StripeSubscriptionId);
        }

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await AddHistoryEntry(subscriptionId, SubscriptionEvent.Cancelled, "Subscription cancelled immediately");

        return true;
    }

    public async Task<bool> PauseSubscriptionAsync(int subscriptionId, string studentId, DateTime resumeDate)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId);
        if (subscription == null || subscription.StudentId != studentId)
            return false;

        // Stripe subscription pausing requires Stripe Billing
        // For now, we'll handle this locally
        subscription.Status = SubscriptionStatus.Paused;
        subscription.PausedAt = DateTime.UtcNow;
        subscription.ResumeDate = resumeDate;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await AddHistoryEntry(subscriptionId, SubscriptionEvent.Paused,
            $"Subscription paused. Resume date: {resumeDate:d}");

        return true;
    }

    public async Task<bool> ResumeSubscriptionAsync(int subscriptionId, string studentId)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId);
        if (subscription == null || subscription.StudentId != studentId)
            return false;

        if (subscription.Status != SubscriptionStatus.Paused)
            return false;

        subscription.Status = SubscriptionStatus.Active;
        subscription.PausedAt = null;
        subscription.ResumeDate = null;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await AddHistoryEntry(subscriptionId, SubscriptionEvent.Resumed, "Subscription resumed");

        return true;
    }

    public async Task<bool> ChangeSubscriptionTierAsync(int subscriptionId, int newTierId)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId);
        if (subscription == null) return false;

        var newTier = await GetSubscriptionTierAsync(newTierId);
        if (newTier == null || string.IsNullOrEmpty(newTier.StripePriceId))
            return false;

        var previousTierId = subscription.SubscriptionTierId;

        // Update in Stripe
        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            var service = new StripeSubService();
            var stripeSubscription = await service.GetAsync(subscription.StripeSubscriptionId);

            await service.UpdateAsync(subscription.StripeSubscriptionId, new Stripe.SubscriptionUpdateOptions
            {
                Items = new List<Stripe.SubscriptionItemOptions>
                {
                    new Stripe.SubscriptionItemOptions
                    {
                        Id = stripeSubscription.Items.Data[0].Id,
                        Price = newTier.StripePriceId
                    }
                },
                ProrationBehavior = "create_prorations"
            });
        }

        subscription.SubscriptionTierId = newTierId;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var eventType = newTier.ClassesPerMonth > (subscription.Tier?.ClassesPerMonth ?? 0)
            ? SubscriptionEvent.Upgraded
            : SubscriptionEvent.Downgraded;

        await AddHistoryEntry(subscriptionId, eventType,
            $"Tier changed from {previousTierId} to {newTierId}",
            previousTierId, newTierId);

        return true;
    }

    public async Task<string?> GetCustomerPortalUrlAsync(string studentId, string returnUrl)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student == null || string.IsNullOrEmpty(student.StripeCustomerId))
            return null;

        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = student.StripeCustomerId,
            ReturnUrl = returnUrl
        };

        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(options);

        return session.Url;
    }

    #endregion

    #region Recurring Schedule Management

    public async Task<bool> SetRecurringScheduleAsync(
        int subscriptionId,
        IEnumerable<RecurringScheduleRequest> schedules)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId);
        if (subscription == null) return false;

        // Remove existing schedules
        var existingSchedules = await _context.RecurringSchedules
            .Where(r => r.SubscriptionId == subscriptionId)
            .ToListAsync();
        _context.RecurringSchedules.RemoveRange(existingSchedules);

        // Add new schedules
        foreach (var request in schedules)
        {
            var schedule = new RecurringSchedule
            {
                SubscriptionId = subscriptionId,
                TimeSlotId = request.TimeSlotId,
                DayOfWeek = request.DayOfWeek,
                Time = request.Time,
                IsActive = true
            };
            _context.RecurringSchedules.Add(schedule);
        }

        await _context.SaveChangesAsync();
        await AddHistoryEntry(subscriptionId, SubscriptionEvent.ScheduleChanged,
            $"Recurring schedule updated with {schedules.Count()} time slots");

        return true;
    }

    public async Task<IEnumerable<RecurringSchedule>> GetRecurringSchedulesAsync(int subscriptionId)
    {
        return await _context.RecurringSchedules
            .Include(r => r.TimeSlot)
            .Where(r => r.SubscriptionId == subscriptionId && r.IsActive)
            .ToListAsync();
    }

    #endregion

    #region Monthly Class Generation

    public async Task GenerateMonthlyClassesAsync()
    {
        var activeSubscriptions = await _context.Subscriptions
            .Include(s => s.RecurringSchedules)
            .Where(s => s.Status == SubscriptionStatus.Active)
            .ToListAsync();

        foreach (var subscription in activeSubscriptions)
        {
            await GenerateClassesForSubscriptionAsync(subscription.Id);
        }
    }

    public async Task GenerateClassesForSubscriptionAsync(int subscriptionId)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Tier)
            .Include(s => s.RecurringSchedules)
            .ThenInclude(r => r.TimeSlot)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (subscription == null || subscription.Status != SubscriptionStatus.Active)
            return;

        var schedules = subscription.RecurringSchedules.Where(r => r.IsActive).ToList();
        if (!schedules.Any()) return;

        // Generate for current month
        var periodStart = subscription.CurrentPeriodStart ?? DateTime.UtcNow;
        var periodEnd = subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);

        foreach (var schedule in schedules)
        {
            var currentDate = periodStart;
            while (currentDate <= periodEnd)
            {
                if (currentDate.DayOfWeek == schedule.DayOfWeek)
                {
                    var classDateTime = currentDate.Date.Add(schedule.Time);

                    // Skip if class already exists
                    var exists = await _context.ScheduledClasses
                        .AnyAsync(sc =>
                            sc.SubscriptionId == subscriptionId &&
                            sc.ClassDateTime == classDateTime);

                    if (!exists && classDateTime > DateTime.UtcNow)
                    {
                        var scheduledClass = new ScheduledClass
                        {
                            StudentId = subscription.StudentId,
                            TimeSlotId = schedule.TimeSlotId,
                            ClassDateTime = classDateTime,
                            Status = ClassStatus.Scheduled,
                            PaymentStatus = PaymentStatus.PartOfSubscription,
                            SubscriptionId = subscriptionId,
                            IsFromRecurringSchedule = true,
                            RecurringScheduleId = schedule.Id
                        };
                        _context.ScheduledClasses.Add(scheduledClass);
                    }
                }
                currentDate = currentDate.AddDays(1);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> UseSubscriptionClassAsync(int subscriptionId, int classId)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId);
        if (subscription == null || subscription.Status != SubscriptionStatus.Active)
            return false;

        if (subscription.ClassesRemainingThisMonth <= 0)
            return false;

        var scheduledClass = await _context.ScheduledClasses.FindAsync(classId);
        if (scheduledClass == null) return false;

        scheduledClass.SubscriptionId = subscriptionId;
        scheduledClass.PaymentStatus = PaymentStatus.PartOfSubscription;
        subscription.ClassesUsedThisMonth++;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Cancellation Policy

    public bool CanCancelThisMonth(Subscription subscription)
    {
        // Can cancel if before 3rd week of the month
        var cancellationDeadlineWeek = _configuration.GetValue<int>("AppSettings:CancellationDeadlineWeek", 3);
        var today = DateTime.UtcNow;
        var weekOfMonth = (today.Day - 1) / 7 + 1;

        return weekOfMonth < cancellationDeadlineWeek;
    }

    public DateTime GetEffectiveCancellationDate(Subscription subscription)
    {
        if (CanCancelThisMonth(subscription))
        {
            // Cancellation takes effect at end of current period
            return subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);
        }
        else
        {
            // Cancellation takes effect at end of next period
            var nextPeriodEnd = (subscription.CurrentPeriodEnd ?? DateTime.UtcNow).AddMonths(1);
            return nextPeriodEnd;
        }
    }

    #endregion

    #region Stripe Customer Management

    public async Task<string> GetOrCreateStripeCustomerAsync(string studentId)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
            throw new ArgumentException("Student not found", nameof(studentId));

        if (!string.IsNullOrEmpty(student.StripeCustomerId))
            return student.StripeCustomerId;

        var options = new CustomerCreateOptions
        {
            Email = student.Email,
            Name = student.FullName,
            Metadata = new Dictionary<string, string>
            {
                { "studentId", studentId }
            }
        };

        var service = new CustomerService();
        var customer = await service.CreateAsync(options);

        student.StripeCustomerId = customer.Id;
        await _context.SaveChangesAsync();

        return customer.Id;
    }

    #endregion

    #region Helper Methods

    private async Task AddHistoryEntry(
        int subscriptionId,
        SubscriptionEvent eventType,
        string? details = null,
        int? previousTierId = null,
        int? newTierId = null)
    {
        var history = new SubscriptionHistory
        {
            SubscriptionId = subscriptionId,
            Event = eventType,
            Details = details,
            PreviousTierId = previousTierId,
            NewTierId = newTierId
        };

        _context.SubscriptionHistory.Add(history);
        await _context.SaveChangesAsync();
    }

    #endregion
}
