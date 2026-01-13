using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class AlertViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string type, string? message, bool dismissible = true)
    {
        if (string.IsNullOrEmpty(message))
        {
            return Content(string.Empty);
        }

        return View(new AlertViewModel
        {
            Type = type?.ToLower() ?? "info",
            Message = message,
            Dismissible = dismissible
        });
    }
}

public class AlertViewModel
{
    public string Type { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public bool Dismissible { get; set; } = true;

    public string BackgroundColor => Type switch
    {
        "success" => "bg-green-100 border-green-400 text-green-700",
        "error" => "bg-red-100 border-red-400 text-red-700",
        "warning" => "bg-yellow-100 border-yellow-400 text-yellow-700",
        "info" => "bg-blue-100 border-blue-400 text-blue-700",
        _ => "bg-gray-100 border-gray-400 text-gray-700"
    };

    public string IconPath => Type switch
    {
        "success" => "M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z",
        "error" => "M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z",
        "warning" => "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z",
        "info" => "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z",
        _ => "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
    };
}
