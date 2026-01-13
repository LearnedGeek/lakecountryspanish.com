using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class LlamaMascotViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string size = "md",
        string mood = "default",
        string? speechText = null,
        bool showBorder = true,
        bool animate = true)
    {
        return View(new LlamaMascotViewModel
        {
            Size = size,
            Mood = mood,
            SpeechText = speechText,
            ShowBorder = showBorder,
            Animate = animate
        });
    }
}

public class LlamaMascotViewModel
{
    /// <summary>
    /// Size: xs (32px), sm (48px), md (80px), lg (120px), xl (200px)
    /// </summary>
    public string Size { get; set; } = "md";

    /// <summary>
    /// Mood affects animation: default, happy, thinking, sad, wave
    /// </summary>
    public string Mood { get; set; } = "default";

    /// <summary>
    /// Custom text for the speech bubble (replaces ñ)
    /// </summary>
    public string? SpeechText { get; set; }

    /// <summary>
    /// Whether to show the circular border
    /// </summary>
    public bool ShowBorder { get; set; } = true;

    /// <summary>
    /// Whether to animate the llama
    /// </summary>
    public bool Animate { get; set; } = true;

    public string SizeClass => Size switch
    {
        "xs" => "h-8 w-8",
        "sm" => "h-12 w-12",
        "md" => "h-20 w-20",
        "lg" => "h-32 w-32",
        "xl" => "h-48 w-48",
        _ => "h-20 w-20"
    };

    public string MoodClass => Mood switch
    {
        "happy" => "llama-happy",
        "thinking" => "llama-thinking",
        "sad" => "llama-sad",
        "wave" => "llama-wave",
        _ => ""
    };

    public string AnimationClass => Animate ? "" : "llama-static";
}
