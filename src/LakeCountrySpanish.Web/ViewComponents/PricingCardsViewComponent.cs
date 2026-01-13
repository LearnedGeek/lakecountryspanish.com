using LakeCountrySpanish.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

/// <summary>
/// ViewComponent for displaying pricing cards consistently across the site.
/// Used on Home page and Subscription/Plans page.
/// </summary>
public class PricingCardsViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        IEnumerable<HomeSubscriptionTierViewModel> tiers,
        bool isLoggedIn = false,
        bool showTrialCard = false,
        int? currentTierId = null,
        bool showScheduleButton = false)
    {
        var model = new PricingCardsViewModel
        {
            Tiers = tiers.ToList(),
            IsLoggedIn = isLoggedIn,
            ShowTrialCard = showTrialCard,
            CurrentTierId = currentTierId,
            ShowScheduleButton = showScheduleButton
        };

        return View(model);
    }
}

/// <summary>
/// ViewModel for the PricingCards ViewComponent.
/// </summary>
public class PricingCardsViewModel
{
    public List<HomeSubscriptionTierViewModel> Tiers { get; set; } = new();
    public bool IsLoggedIn { get; set; }
    public bool ShowTrialCard { get; set; }
    public int? CurrentTierId { get; set; }
    public bool ShowScheduleButton { get; set; }
}
