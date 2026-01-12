namespace LakeCountrySpanish.Web.Models.Entities;

public enum PaymentStatusType
{
    Pending,
    Completed,
    Failed,
    Refunded
}

public enum PaymentType
{
    SingleClass,
    Package,
    Subscription,
    TokenPurchase
}

public class Payment
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? StripeSessionId { get; set; }
    public PaymentStatusType Status { get; set; } = PaymentStatusType.Pending;
    public PaymentType PaymentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Subscription ID if this payment is for a subscription
    /// </summary>
    public int? SubscriptionId { get; set; }

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual ICollection<ScheduledClass> ScheduledClasses { get; set; } = new List<ScheduledClass>();
    public virtual StudentPackage? StudentPackage { get; set; }
    public virtual Subscription? Subscription { get; set; }
}
