namespace LakeCountrySpanish.Web.Services.Media;

/// <summary>
/// Strongly-typed binding for the "Pixabay" config section. The real key
/// lives in appsettings.Local.json (gitignored) per the SpanishScheduler
/// secrets convention; appsettings.json holds an empty placeholder so
/// developers see what config the app expects.
/// </summary>
public sealed class PixabaySettings
{
    public const string SectionName = "Pixabay";

    public string ApiKey { get; set; } = string.Empty;
}
