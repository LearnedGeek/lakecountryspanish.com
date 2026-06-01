namespace LakeCountrySpanish.Web.Services.Media;

/// <summary>
/// Strongly-typed binding for the "Replicate" config section. The real token
/// lives in appsettings.Local.json (gitignored); appsettings.json holds an
/// empty placeholder.
/// </summary>
public sealed class ReplicateSettings
{
    public const string SectionName = "Replicate";

    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Replicate model slug, e.g. "black-forest-labs/flux-schnell". Lives in
    /// config so the model can be swapped without code changes when a better
    /// or cheaper model ships.
    /// </summary>
    public string Model { get; set; } = "black-forest-labs/flux-schnell";
}
