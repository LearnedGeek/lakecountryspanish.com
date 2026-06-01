namespace LakeCountrySpanish.Web.Services.Media;

/// <summary>
/// Prompt-based image generator (Replicate, DALL-E, etc.). Distinct from
/// <see cref="IImageSourceAdapter"/>, which is for search-and-import sources
/// like Pixabay or Unsplash where you find an image rather than generate one.
///
/// Returns a <see cref="DownloadedImage"/> so callers can hand it to the
/// existing media pipeline (upload as a MediaAsset with Source=AIGenerated,
/// AiPrompt + AiModel populated).
/// </summary>
public interface IAiImageService
{
    /// <summary>Display name, e.g. "Replicate".</summary>
    string ProviderName { get; }

    /// <summary>Model identifier stored on the resulting MediaAsset, e.g. "flux-schnell".</summary>
    string ModelId { get; }

    /// <summary>False when API token is missing; UI hides the "Generate" action.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Generate a single image from a prompt. Throws on API error or timeout.
    /// </summary>
    Task<DownloadedImage> GenerateAsync(string prompt, CancellationToken ct = default);
}
