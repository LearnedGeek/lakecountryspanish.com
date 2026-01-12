namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Defines a subscription plan tier (e.g., 2, 4, or 8 classes per month).
/// </summary>
public class SubscriptionTier
{
    public int Id { get; set; }

    /// <summary>
    /// Display name (e.g., "Starter", "Standard", "Premium")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the tier benefits
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Number of classes included per month (2, 4, 8, etc.)
    /// </summary>
    public int ClassesPerMonth { get; set; }

    /// <summary>
    /// Monthly subscription price
    /// </summary>
    public decimal MonthlyPrice { get; set; }

    /// <summary>
    /// Calculated price per class for display
    /// </summary>
    public decimal PerClassPrice => ClassesPerMonth > 0 ? MonthlyPrice / ClassesPerMonth : 0;

    /// <summary>
    /// Points multiplier for subscribers (e.g., 1.5 for 50% bonus)
    /// </summary>
    public decimal PointsMultiplier { get; set; } = 1.5m;

    /// <summary>
    /// Stripe Product ID for this tier
    /// </summary>
    public string? StripeProductId { get; set; }

    /// <summary>
    /// Stripe Price ID (recurring monthly price)
    /// </summary>
    public string? StripePriceId { get; set; }

    /// <summary>
    /// Display order on pricing page
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether this tier is currently available for purchase
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
