using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Request model for setting recurring schedules
/// </summary>
public class RecurringScheduleRequest
{
    public int TimeSlotId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan Time { get; set; }
}

/// <summary>
/// Service for managing student subscriptions.
/// </summary>
public interface ISubscriptionService
{
    // Tier queries
    /// <summary>
    /// Gets all active subscription tiers ordered by display order.
    /// </summary>
    Task<IEnumerable<SubscriptionTier>> GetActiveSubscriptionTiersAsync();

    /// <summary>
    /// Gets a specific subscription tier by ID.
    /// </summary>
    Task<SubscriptionTier?> GetSubscriptionTierAsync(int tierId);

    // Subscription queries
    /// <summary>
    /// Gets the active subscription for a student (if any).
    /// </summary>
    Task<Subscription?> GetActiveSubscriptionAsync(string studentId);

    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    Task<Subscription?> GetSubscriptionAsync(int subscriptionId);

    /// <summary>
    /// Gets all subscriptions for a student (including cancelled/expired).
    /// </summary>
    Task<IEnumerable<Subscription>> GetStudentSubscriptionsAsync(string studentId);

    // Subscription creation
    /// <summary>
    /// Creates a Stripe Checkout session for starting a subscription.
    /// </summary>
    /// <returns>The Checkout Session URL to redirect to.</returns>
    Task<string> CreateSubscriptionCheckoutAsync(string studentId, int tierId, string successUrl, string cancelUrl);

    /// <summary>
    /// Processes a Stripe subscription webhook event.
    /// </summary>
    Task<bool> ProcessSubscriptionWebhookAsync(string json, string signature);

    // Subscription management
    /// <summary>
    /// Requests cancellation of a subscription (takes effect at end of period or after notice).
    /// </summary>
    Task<bool> RequestCancellationAsync(int subscriptionId, string studentId);

    /// <summary>
    /// Immediately cancels a subscription (admin function).
    /// </summary>
    Task<bool> CancelSubscriptionAsync(int subscriptionId, string studentId);

    /// <summary>
    /// Pauses a subscription until a specified date.
    /// </summary>
    Task<bool> PauseSubscriptionAsync(int subscriptionId, string studentId, DateTime resumeDate);

    /// <summary>
    /// Resumes a paused subscription.
    /// </summary>
    Task<bool> ResumeSubscriptionAsync(int subscriptionId, string studentId);

    /// <summary>
    /// Changes subscription to a different tier.
    /// </summary>
    Task<bool> ChangeSubscriptionTierAsync(int subscriptionId, int newTierId);

    /// <summary>
    /// Gets the Stripe Customer Portal URL for managing billing.
    /// </summary>
    Task<string?> GetCustomerPortalUrlAsync(string studentId, string returnUrl);

    // Recurring schedule management
    /// <summary>
    /// Sets the recurring class schedule for a subscription.
    /// </summary>
    Task<bool> SetRecurringScheduleAsync(int subscriptionId, IEnumerable<RecurringScheduleRequest> schedules);

    /// <summary>
    /// Gets the recurring schedules for a subscription.
    /// </summary>
    Task<IEnumerable<RecurringSchedule>> GetRecurringSchedulesAsync(int subscriptionId);

    // Monthly class generation
    /// <summary>
    /// Generates scheduled classes for all active subscriptions for the current/next month.
    /// Called by background job on 1st of month or subscription renewal.
    /// </summary>
    Task GenerateMonthlyClassesAsync();

    /// <summary>
    /// Generates classes for a specific subscription (e.g., when subscription is created).
    /// </summary>
    Task GenerateClassesForSubscriptionAsync(int subscriptionId);

    /// <summary>
    /// Uses a class credit from a subscription for a scheduled class.
    /// </summary>
    Task<bool> UseSubscriptionClassAsync(int subscriptionId, int classId);

    // Cancellation policy
    /// <summary>
    /// Checks if a subscription can be cancelled this month (before 3rd week deadline).
    /// </summary>
    bool CanCancelThisMonth(Subscription subscription);

    /// <summary>
    /// Gets the effective cancellation date based on the cancellation policy.
    /// </summary>
    DateTime GetEffectiveCancellationDate(Subscription subscription);

    // Stripe customer management
    /// <summary>
    /// Gets or creates a Stripe Customer ID for a student.
    /// </summary>
    Task<string> GetOrCreateStripeCustomerAsync(string studentId);
}
