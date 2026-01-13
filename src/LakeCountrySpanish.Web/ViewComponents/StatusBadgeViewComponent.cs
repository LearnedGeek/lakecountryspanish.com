using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class StatusBadgeViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string text, string color = "gray", string size = "sm")
    {
        return View(new StatusBadgeViewModel
        {
            Text = text,
            Color = color.ToLower(),
            Size = size.ToLower()
        });
    }
}

public class StatusBadgeViewModel
{
    public string Text { get; set; } = string.Empty;
    public string Color { get; set; } = "gray";
    public string Size { get; set; } = "sm";

    public string ColorClasses => Color switch
    {
        "green" => "bg-green-100 text-green-800",
        "red" => "bg-red-100 text-red-800",
        "yellow" => "bg-yellow-100 text-yellow-800",
        "blue" => "bg-blue-100 text-blue-800",
        "indigo" => "bg-indigo-100 text-indigo-800",
        "purple" => "bg-purple-100 text-purple-800",
        "pink" => "bg-pink-100 text-pink-800",
        "cyan" => "bg-cyan-100 text-cyan-800",
        "orange" => "bg-orange-100 text-orange-800",
        _ => "bg-gray-100 text-gray-800"
    };

    public string SizeClasses => Size switch
    {
        "xs" => "px-1.5 py-0.5 text-xs",
        "md" => "px-3 py-1 text-sm",
        "lg" => "px-4 py-1.5 text-base",
        _ => "px-2.5 py-0.5 text-xs"
    };

    public string AllClasses => $"inline-flex items-center rounded-full font-medium {ColorClasses} {SizeClasses}";
}
