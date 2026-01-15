using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

/// <summary>
/// ViewComponent for displaying lessons in a Preply-style list view.
/// Shows confirmed classes with green checkmarks and reserved (future) classes with amber info.
/// </summary>
public class LessonsListViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        IEnumerable<ScheduledClass> upcomingClasses,
        IEnumerable<ReservedClassViewModel> reservedClasses,
        Subscription? subscription)
    {
        var model = new LessonsListViewModel
        {
            UpcomingClasses = upcomingClasses?.ToList() ?? new List<ScheduledClass>(),
            ReservedClasses = reservedClasses?.ToList() ?? new List<ReservedClassViewModel>(),
            Subscription = subscription,
            NextBillingDate = subscription?.CurrentPeriodEnd
        };

        return View(model);
    }
}
