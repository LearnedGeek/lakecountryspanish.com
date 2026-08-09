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
    /// The soonest listed program with an open enrollment window — surfaced as
    /// a "Now Enrolling" hero strip on the homepage. Null when no listed
    /// program is currently accepting signups. Karen changes what's featured
    /// by adjusting program start dates or the IsListed flag; no separate
    /// "featured" admin toggle needed.
    /// </summary>
    public EnrollmentProgram? FeaturedProgram { get; set; }
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
