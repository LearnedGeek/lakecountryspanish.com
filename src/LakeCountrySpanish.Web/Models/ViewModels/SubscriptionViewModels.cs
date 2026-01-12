using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// View model for the subscription plans page.
/// </summary>
public class SubscriptionPlansViewModel
{
    public IEnumerable<SubscriptionTier> Tiers { get; set; } = Enumerable.Empty<SubscriptionTier>();
    public Subscription? CurrentSubscription { get; set; }
}

/// <summary>
/// View model for the subscription management page.
/// </summary>
public class SubscriptionManageViewModel
{
    public Subscription? CurrentSubscription { get; set; }
    public IEnumerable<Subscription> AllSubscriptions { get; set; } = Enumerable.Empty<Subscription>();
    public IEnumerable<RecurringSchedule> RecurringSchedules { get; set; } = Enumerable.Empty<RecurringSchedule>();
    public IEnumerable<TimeSlot> AvailableTimeSlots { get; set; } = Enumerable.Empty<TimeSlot>();
    public bool CanCancelThisMonth { get; set; }
    public DateTime? EffectiveCancellationDate { get; set; }

    /// <summary>
    /// Gets the classes remaining this month for display.
    /// </summary>
    public int ClassesRemaining => CurrentSubscription?.ClassesRemainingThisMonth ?? 0;

    /// <summary>
    /// Gets the total classes per month for the current tier.
    /// </summary>
    public int ClassesPerMonth => CurrentSubscription?.Tier?.ClassesPerMonth ?? 0;

    /// <summary>
    /// Gets whether the subscription is currently paused.
    /// </summary>
    public bool IsPaused => CurrentSubscription?.Status == SubscriptionStatus.Paused;

    /// <summary>
    /// Gets whether the subscription has pending cancellation.
    /// </summary>
    public bool IsCancellationPending => CurrentSubscription?.CancellationRequested == true;

    /// <summary>
    /// Gets whether the subscription is active.
    /// </summary>
    public bool IsActive => CurrentSubscription?.Status == SubscriptionStatus.Active;

    /// <summary>
    /// Gets the subscription status display text.
    /// </summary>
    public string StatusDisplay => CurrentSubscription?.Status switch
    {
        SubscriptionStatus.Active when IsCancellationPending => "Active (Cancelling)",
        SubscriptionStatus.Active => "Active",
        SubscriptionStatus.Paused => "Paused",
        SubscriptionStatus.PastDue => "Past Due",
        SubscriptionStatus.Cancelled => "Cancelled",
        SubscriptionStatus.Expired => "Expired",
        _ => "No Subscription"
    };

    /// <summary>
    /// Gets the status badge CSS class.
    /// </summary>
    public string StatusBadgeClass => CurrentSubscription?.Status switch
    {
        SubscriptionStatus.Active when IsCancellationPending => "badge-warning",
        SubscriptionStatus.Active => "badge-success",
        SubscriptionStatus.Paused => "badge-info",
        SubscriptionStatus.PastDue => "badge-danger",
        SubscriptionStatus.Cancelled => "badge-secondary",
        SubscriptionStatus.Expired => "badge-dark",
        _ => "badge-light"
    };
}

/// <summary>
/// View model for subscription tier selection/display.
/// </summary>
public class SubscriptionTierViewModel
{
    public SubscriptionTier Tier { get; set; } = null!;
    public bool IsCurrentTier { get; set; }
    public bool IsRecommended { get; set; }

    /// <summary>
    /// Gets the price per class for this tier.
    /// </summary>
    public decimal PricePerClass => Tier.ClassesPerMonth > 0
        ? Tier.MonthlyPrice / Tier.ClassesPerMonth
        : 0;

    /// <summary>
    /// Gets the savings percentage compared to single class pricing.
    /// </summary>
    public int SavingsPercent(decimal singleClassPrice)
    {
        if (singleClassPrice <= 0 || Tier.ClassesPerMonth <= 0)
            return 0;

        var fullPrice = singleClassPrice * Tier.ClassesPerMonth;
        var savings = (1 - (Tier.MonthlyPrice / fullPrice)) * 100;
        return (int)Math.Round(savings);
    }
}
