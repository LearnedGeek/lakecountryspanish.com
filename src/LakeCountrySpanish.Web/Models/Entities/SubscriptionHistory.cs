namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Types of subscription events for audit trail
/// </summary>
public enum SubscriptionEvent
{
    /// <summary>Subscription created</summary>
    Created,
    /// <summary>Monthly renewal processed</summary>
    Renewed,
    /// <summary>Upgraded to higher tier</summary>
    Upgraded,
    /// <summary>Downgraded to lower tier</summary>
    Downgraded,
    /// <summary>Subscription paused</summary>
    Paused,
    /// <summary>Subscription resumed from pause</summary>
    Resumed,
    /// <summary>Cancellation requested</summary>
    CancellationRequested,
    /// <summary>Subscription cancelled</summary>
    Cancelled,
    /// <summary>Payment failed</summary>
    PaymentFailed,
    /// <summary>Payment succeeded</summary>
    PaymentSucceeded,
    /// <summary>Classes reset for new period</summary>
    ClassesReset,
    /// <summary>Recurring schedule changed</summary>
    ScheduleChanged
}

/// <summary>
/// Audit trail for subscription changes.
/// </summary>
public class SubscriptionHistory
{
    public int Id { get; set; }

    /// <summary>
    /// The subscription this event belongs to
    /// </summary>
    public int SubscriptionId { get; set; }

    /// <summary>
    /// Type of event that occurred
    /// </summary>
    public SubscriptionEvent Event { get; set; }

    /// <summary>
    /// Additional details as JSON (e.g., Stripe event data)
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Previous tier ID (for upgrades/downgrades)
    /// </summary>
    public int? PreviousTierId { get; set; }

    /// <summary>
    /// New tier ID (for upgrades/downgrades)
    /// </summary>
    public int? NewTierId { get; set; }

    /// <summary>
    /// When this event occurred
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Subscription Subscription { get; set; } = null!;
}
