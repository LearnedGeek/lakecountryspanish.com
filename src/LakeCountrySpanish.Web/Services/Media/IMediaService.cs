using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services.Media;

/// <summary>
/// High-level orchestration on the media library. Wraps the EF entities, the
/// image processing service, and the source adapters so controllers don't
/// touch storage details directly.
/// </summary>
public interface IMediaService
{
    /// <summary>Pages through the library, newest first. Optional category filter.</summary>
    Task<MediaLibraryPage> ListAsync(int page, int pageSize, MediaCategory? category = null, MediaSource? source = null, CancellationToken ct = default);

    Task<MediaAsset?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Persists an uploaded image to disk + creates a MediaAsset row. If a
    /// row with the same file hash already exists, returns the existing one
    /// rather than duplicating.
    /// </summary>
    Task<MediaAsset> UploadAsync(UploadMediaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Downloads the given search hit through its adapter and persists it.
    /// Dedup is by file hash — re-importing an already-saved image is a no-op
    /// that returns the existing row.
    /// </summary>
    Task<MediaAsset> ImportFromSourceAsync(IImageSourceAdapter adapter, ImageSearchHit hit, ImportMediaOptions options, CancellationToken ct = default);

    /// <summary>
    /// Hard-delete an asset and its file. Fails if the asset is currently
    /// referenced by any <see cref="MediaUsage"/> row.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class UploadMediaRequest
{
    public byte[] Bytes { get; init; } = Array.Empty<byte>();
    public string OriginalFileName { get; init; } = string.Empty;
    public string MimeType { get; init; } = "application/octet-stream";
    public MediaCategory Category { get; init; } = MediaCategory.Uncategorized;
    public string? Title { get; init; }
    public string? AltText { get; init; }
    public string? Tags { get; init; }
    public string UploadedById { get; init; } = string.Empty;
}

public sealed class ImportMediaOptions
{
    public MediaCategory Category { get; init; } = MediaCategory.Uncategorized;
    public string? Title { get; init; }
    public string? AltText { get; init; }
    public string? Tags { get; init; }
    public string ImportedById { get; init; } = string.Empty;
}

public sealed record MediaLibraryPage(
    IReadOnlyList<MediaAsset> Items,
    int Page,
    int PageSize,
    int TotalItems);
