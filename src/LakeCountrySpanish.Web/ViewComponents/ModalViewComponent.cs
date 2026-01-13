using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class ModalViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string id,
        string title,
        string size = "md",
        bool closeOnBackdrop = true)
    {
        return View(new ModalViewModel
        {
            Id = id,
            Title = title,
            Size = size.ToLower(),
            CloseOnBackdrop = closeOnBackdrop
        });
    }
}

public class ModalViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Size { get; set; } = "md";
    public bool CloseOnBackdrop { get; set; } = true;

    public string SizeClass => Size switch
    {
        "sm" => "max-w-sm",
        "lg" => "max-w-2xl",
        "xl" => "max-w-4xl",
        "full" => "max-w-full mx-4",
        _ => "max-w-lg"
    };
}
