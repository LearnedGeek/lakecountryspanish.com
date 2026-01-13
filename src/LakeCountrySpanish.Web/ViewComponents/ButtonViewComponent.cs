using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class ButtonViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string text,
        string type = "primary",
        string size = "md",
        string? icon = null,
        string? href = null,
        bool submit = false,
        string? onclick = null,
        bool disabled = false,
        string? cssClass = null)
    {
        return View(new ButtonViewModel
        {
            Text = text,
            Type = type.ToLower(),
            Size = size.ToLower(),
            Icon = icon,
            Href = href,
            Submit = submit,
            OnClick = onclick,
            Disabled = disabled,
            CssClass = cssClass
        });
    }
}

public class ButtonViewModel
{
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "primary";
    public string Size { get; set; } = "md";
    public string? Icon { get; set; }
    public string? Href { get; set; }
    public bool Submit { get; set; }
    public string? OnClick { get; set; }
    public bool Disabled { get; set; }
    public string? CssClass { get; set; }

    public bool IsLink => !string.IsNullOrEmpty(Href);

    public string BaseClasses => "inline-flex items-center justify-center font-medium rounded-md transition focus:outline-none focus:ring-2 focus:ring-offset-2";

    public string SizeClasses => Size switch
    {
        "sm" => "px-3 py-1.5 text-xs",
        "lg" => "px-6 py-3 text-base",
        _ => "px-4 py-2 text-sm"
    };

    public string TypeClasses => Type switch
    {
        "primary" => "border border-transparent text-white bg-indigo-600 hover:bg-indigo-700 focus:ring-indigo-500",
        "secondary" => "border border-gray-300 text-gray-700 bg-white hover:bg-gray-50 focus:ring-indigo-500",
        "danger" => "border border-transparent text-white bg-red-600 hover:bg-red-700 focus:ring-red-500",
        "success" => "border border-transparent text-white bg-green-600 hover:bg-green-700 focus:ring-green-500",
        "warning" => "border border-transparent text-white bg-yellow-600 hover:bg-yellow-700 focus:ring-yellow-500",
        "ghost" => "border border-transparent text-gray-600 hover:text-gray-900 hover:bg-gray-100 focus:ring-gray-500",
        _ => "border border-gray-300 text-gray-700 bg-white hover:bg-gray-50 focus:ring-indigo-500"
    };

    public string DisabledClasses => Disabled ? "opacity-50 cursor-not-allowed" : "";

    public string AllClasses => $"{BaseClasses} {SizeClasses} {TypeClasses} {DisabledClasses} {CssClass}".Trim();

    public string? IconSvgPath => Icon switch
    {
        "plus" => "M12 4.5v15m7.5-7.5h-15",
        "arrow-left" => "M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18",
        "check" => "M4.5 12.75l6 6 9-13.5",
        "x" => "M6 18L18 6M6 6l12 12",
        "edit" => "M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10",
        "trash" => "M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0",
        "save" => "M17.593 3.322c1.1.128 1.907 1.077 1.907 2.185V21L12 17.25 4.5 21V5.507c0-1.108.806-2.057 1.907-2.185a48.507 48.507 0 0111.186 0z",
        "download" => "M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3",
        "upload" => "M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5",
        "eye" => "M2.036 12.322a1.012 1.012 0 010-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178z M15 12a3 3 0 11-6 0 3 3 0 016 0z",
        "user-plus" => "M19 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zM4 19.235v-.11a6.375 6.375 0 0112.75 0v.109A12.318 12.318 0 0110.374 21c-2.331 0-4.512-.645-6.374-1.766z",
        _ => null
    };
}
