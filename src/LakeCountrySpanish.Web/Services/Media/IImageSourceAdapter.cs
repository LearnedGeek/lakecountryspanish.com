using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services.Media;

/// <summary>
/// Adapter abstraction over an external image source (Pixabay, Unsplash, etc.).
/// Lets the media library uniformly search, preview, and import images from
/// multiple providers. Phase 3 ships with one implementation (Pixabay);
/// additional adapters slot in without controller changes.
/// </summary>
public interface IImageSourceAdapter
{
    /// <summary>Human-readable source name shown in the UI ("Pixabay", "Unsplash").</summary>
    string SourceName { get; }

    /// <summary>The <see cref="MediaSource"/> enum value this adapter populates.</summary>
    MediaSource SourceEnum { get; }

    /// <summary>
    /// False when the adapter cannot operate (API key missing, etc.). The UI
    /// hides search tabs for unavailable sources rather than throwing later.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Free-text image search, paginated.</summary>
    Task<ImageSearchResults> SearchAsync(ImageSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// Downloads the full-size image bytes for a specific search hit. The hit
    /// carries the source-specific URL so callers don't need to know about it.
    /// </summary>
    Task<DownloadedImage> DownloadAsync(ImageSearchHit hit, CancellationToken ct = default);
}

public sealed record ImageSearchQuery(
    string Query,
    int Page = 1,
    int PerPage = 24,
    ImageContentType ContentType = ImageContentType.All);

public enum ImageContentType
{
    All,
    Photo,
    Illustration,
    Vector
}

public sealed record ImageSearchResults(
    int Total,
    int TotalHits,
    int Page,
    IReadOnlyList<ImageSearchHit> Hits);

/// <summary>
/// One image result from a search. The full-size URL is what callers download
/// when importing; the preview/web URLs are for the search-result grid.
/// </summary>
public sealed record ImageSearchHit(
    string SourceId,
    string PageUrl,
    string PreviewUrl,
    string WebUrl,
    string FullUrl,
    int Width,
    int Height,
    string Photographer,
    string PhotographerUrl,
    string LicenseType,
    IReadOnlyList<string> Tags);

/// <summary>Downloaded image bytes + the inferred MIME and filename hint.</summary>
public sealed record DownloadedImage(
    byte[] Bytes,
    string MimeType,
    string SuggestedFileName);
