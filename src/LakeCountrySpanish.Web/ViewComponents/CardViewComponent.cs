using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class CardViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string? title = null,
        bool noPadding = false,
        string? headerAction = null,
        string? headerActionUrl = null,
        string? cssClass = null)
    {
        return View(new CardViewModel
        {
            Title = title,
            NoPadding = noPadding,
            HeaderAction = headerAction,
            HeaderActionUrl = headerActionUrl,
            CssClass = cssClass
        });
    }
}

public class CardViewModel
{
    public string? Title { get; set; }
    public bool NoPadding { get; set; }
    public string? HeaderAction { get; set; }
    public string? HeaderActionUrl { get; set; }
    public string? CssClass { get; set; }

    public bool HasHeader => !string.IsNullOrEmpty(Title);
    public bool HasHeaderAction => !string.IsNullOrEmpty(HeaderAction) && !string.IsNullOrEmpty(HeaderActionUrl);
    public string BodyPadding => NoPadding ? "" : "p-6";
}
