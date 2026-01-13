using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class PageHeaderViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string title,
        string? subtitle = null,
        string? backUrl = null,
        string? backText = null)
    {
        return View(new PageHeaderViewModel
        {
            Title = title,
            Subtitle = subtitle,
            BackUrl = backUrl,
            BackText = backText ?? "Back"
        });
    }
}

public class PageHeaderViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? BackUrl { get; set; }
    public string BackText { get; set; } = "Back";

    public bool HasBackLink => !string.IsNullOrEmpty(BackUrl);
}
