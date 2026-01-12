namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Subscription status values
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Active and in good standing</summary>
    Active,
    /// <summary>Temporarily paused by student request</summary>
    Paused,
    /// <summary>Payment failed, grace period</summary>
    PastDue,
    /// <summary>Cancelled (either by student or due to non-payment)</summary>
    Cancelled,
    /// <summary>Subscription period ended</summary>
    Expired
}

/// <summary>
/// A student's subscription to a monthly class plan.
/// </summary>
public class Subscription
{
    public int Id { get; set; }

    /// <summary>
    /// The student who owns this subscription
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// The subscription tier (2/4/8 classes per month)
    /// </summary>
    public int SubscriptionTierId { get; set; }

    // Stripe Integration
    /// <summary>
    /// Stripe Subscription ID
    /// </summary>
    public string? StripeSubscriptionId { get; set; }

    /// <summary>
    /// Stripe Customer ID (also stored on ApplicationUser)
    /// </summary>
    public string? StripeCustomerId { get; set; }

    // Status
    /// <summary>
    /// Current subscription status
    /// </summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    /// <summary>
    /// When the subscription started
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Start of current billing period
    /// </summary>
    public DateTime? CurrentPeriodStart { get; set; }

    /// <summary>
    /// End of current billing period
    /// </summary>
    public DateTime? CurrentPeriodEnd { get; set; }

    /// <summary>
    /// When subscription was cancelled (if applicable)
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// When subscription was paused (if applicable)
    /// </summary>
    public DateTime? PausedAt { get; set; }

    /// <summary>
    /// Scheduled resume date (if paused)
    /// </summary>
    public DateTime? ResumeDate { get; set; }

    // Monthly class tracking
    /// <summary>
    /// Number of classes used in the current billing period
    /// </summary>
    public int ClassesUsedThisMonth { get; set; }

    /// <summary>
    /// Classes remaining in current billing period
    /// </summary>
    public int ClassesRemainingThisMonth => Tier?.ClassesPerMonth - ClassesUsedThisMonth ?? 0;

    // Cancellation policy
    /// <summary>
    /// Whether cancellation has been requested (pending end of period)
    /// </summary>
    public bool CancellationRequested { get; set; }

    /// <summary>
    /// When cancellation was requested
    /// </summary>
    public DateTime? CancellationRequestDate { get; set; }

    /// <summary>
    /// When cancellation takes effect (end of notice period)
    /// </summary>
    public DateTime? EffectiveCancellationDate { get; set; }

    /// <summary>
    /// When this record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual SubscriptionTier Tier { get; set; } = null!;
    public virtual ICollection<RecurringSchedule> RecurringSchedules { get; set; } = new List<RecurringSchedule>();
    public virtual ICollection<SubscriptionHistory> History { get; set; } = new List<SubscriptionHistory>();
    public virtual ICollection<ScheduledClass> ScheduledClasses { get; set; } = new List<ScheduledClass>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
