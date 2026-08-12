using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// View model for the Home page displaying subscription pricing and testimonials.
/// </summary>
public class HomeViewModel
{
    public IEnumerable<HomeSubscriptionTierViewModel> SubscriptionTiers { get; set; } = new List<HomeSubscriptionTierViewModel>();
    public IEnumerable<TestimonialDisplayViewModel> Testimonials { get; set; } = new List<TestimonialDisplayViewModel>();

    /// <summary>
    /// Distinct listed programs whose enrollment is currently open, one per
    /// program Name. When Karen runs multiple instances of the same program
    /// (e.g. "Bailamos" at six schools), each Name gets ONE tile; clicking
    /// it takes the parent to /programs to pick the location that fits.
    /// Empty when no listed program is accepting signups.
    /// </summary>
    public IReadOnlyList<FeaturedProgramCard> FeaturedPrograms { get; set; } = Array.Empty<FeaturedProgramCard>();
}

/// <summary>
/// Home-page "Now Enrolling" tile: one distinct program (by Name), with a
/// count and summary of how many instances are currently enrolling.
/// </summary>
public class FeaturedProgramCard
{
    /// <summary>The soonest-starting instance of this program — used for image, dates, price display.</summary>
    public EnrollmentProgram Representative { get; set; } = null!;

    /// <summary>How many instances (rows) of this program name are currently enrolling.</summary>
    public int InstanceCount { get; set; }

    /// <summary>
    /// Ready-to-display string: single location name when only one instance,
    /// "N locations enrolling now" when multiple.
    /// </summary>
    public string LocationSummary { get; set; } = string.Empty;
}

/// <summary>
/// Simplified subscription tier for Home page display.
/// </summary>
public class HomeSubscriptionTierViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ClassesPerMonth { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal PerClassPrice => ClassesPerMonth > 0 ? MonthlyPrice / ClassesPerMonth : 0;
    public bool IsPopular { get; set; }
    public bool IsBestValue { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Calculates savings compared to single class rate ($30/class for tokens).
    /// </summary>
    public decimal SavingsPercent
    {
        get
        {
            const decimal singleClassRate = 30m;
            if (PerClassPrice >= singleClassRate) return 0;
            return Math.Round((1 - (PerClassPrice / singleClassRate)) * 100);
        }
    }

    /// <summary>
    /// Monthly savings compared to paying single class rate.
    /// </summary>
    public decimal MonthlySavings
    {
        get
        {
            const decimal singleClassRate = 30m;
            return (singleClassRate * ClassesPerMonth) - MonthlyPrice;
        }
    }

    /// <summary>
    /// Frequency description (e.g., "1x per week", "2x per week")
    /// </summary>
    public string FrequencyDescription
    {
        get
        {
            return ClassesPerMonth switch
            {
                1 => "1 class",
                2 => "Twice monthly",
                3 => "3x monthly",
                4 => "1x per week",
                6 => "1-2x per week",
                8 => "2x per week",
                12 => "3x per week",
                _ => $"{ClassesPerMonth} classes/month"
            };
        }
    }
}
