using System.Text.Json;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Services.Media;

public sealed class MediaService : IMediaService
{
    /// <summary>Web-relative root under wwwroot/ where imports/uploads land.</summary>
    private const string MediaRoot = "uploads/media";
    /// <summary>Sub-path for generated thumbnails.</summary>
    private const string ThumbnailSubPath = "thumbnails";

    private readonly ApplicationDbContext _context;
    private readonly IImageProcessingService _imaging;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MediaService> _logger;

    public MediaService(
        ApplicationDbContext context,
        IImageProcessingService imaging,
        IWebHostEnvironment environment,
        ILogger<MediaService> logger)
    {
        _context = context;
        _imaging = imaging;
        _environment = environment;
        _logger = logger;
    }

    public async Task<MediaLibraryPage> ListAsync(
        int page, int pageSize,
        MediaCategory? category = null, MediaSource? source = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.MediaAssets.AsNoTracking().AsQueryable();
        if (category.HasValue) query = query.Where(m => m.Category == category.Value);
        if (source.HasValue)   query = query.Where(m => m.Source == source.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.UploadedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new MediaLibraryPage(items, page, pageSize, total);
    }

    public Task<MediaAsset?> GetAsync(int id, CancellationToken ct = default) =>
        _context.MediaAssets.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<MediaAsset> UploadAsync(UploadMediaRequest request, CancellationToken ct = default)
    {
        var hash = _imaging.ComputeSha256(request.Bytes);
        var existing = await _context.MediaAssets.FirstOrDefaultAsync(m => m.FileHash == hash, ct);
        if (existing is not null) return existing;

        var (width, height) = _imaging.GetDimensions(request.Bytes);
        var (relativePath, _) = await PersistToDiskAsync("upload", hash, request.Bytes, request.MimeType, ct);
        var thumbnails = await GenerateThumbnailsAsync(hash, request.Bytes, ct);

        var asset = new MediaAsset
        {
            FileName = request.OriginalFileName,
            FilePath = relativePath,
            Source = MediaSource.Upload,
            Category = request.Category,
            LicenseType = "Internal LCS",
            FileSize = request.Bytes.LongLength,
            MimeType = request.MimeType,
            AltText = request.AltText,
            Title = request.Title ?? Path.GetFileNameWithoutExtension(request.OriginalFileName),
            Tags = request.Tags,
            Width = width,
            Height = height,
            FileHash = hash,
            ThumbnailsJson = JsonSerializer.Serialize(thumbnails),
            UploadedDate = DateTime.UtcNow,
            UploadedById = request.UploadedById
        };

        _context.MediaAssets.Add(asset);
        await _context.SaveChangesAsync(ct);
        return asset;
    }

    public async Task<MediaAsset> ImportFromSourceAsync(
        IImageSourceAdapter adapter, ImageSearchHit hit,
        ImportMediaOptions options, CancellationToken ct = default)
    {
        var download = await adapter.DownloadAsync(hit, ct);

        var hash = _imaging.ComputeSha256(download.Bytes);
        var existing = await _context.MediaAssets.FirstOrDefaultAsync(m => m.FileHash == hash, ct);
        if (existing is not null) return existing;

        var (width, height) = _imaging.GetDimensions(download.Bytes);
        var sourceFolder = adapter.SourceEnum.ToString().ToLowerInvariant();
        var (relativePath, _) = await PersistToDiskAsync(sourceFolder, hash, download.Bytes, download.MimeType, ct);
        var thumbnails = await GenerateThumbnailsAsync(hash, download.Bytes, ct);

        var asset = new MediaAsset
        {
            FileName = download.SuggestedFileName,
            FilePath = relativePath,
            Source = adapter.SourceEnum,
            Category = options.Category,
            SourceId = hit.SourceId,
            SourceUrl = hit.PageUrl,
            Photographer = hit.Photographer,
            PhotographerUrl = hit.PhotographerUrl,
            LicenseType = hit.LicenseType,
            FileSize = download.Bytes.LongLength,
            MimeType = download.MimeType,
            AltText = options.AltText,
            Title = options.Title ?? $"{adapter.SourceName} {hit.SourceId}",
            Tags = options.Tags ?? string.Join(", ", hit.Tags),
            Width = width,
            Height = height,
            FileHash = hash,
            ThumbnailsJson = JsonSerializer.Serialize(thumbnails),
            UploadedDate = DateTime.UtcNow,
            UploadedById = options.ImportedById
        };

        _context.MediaAssets.Add(asset);
        await _context.SaveChangesAsync(ct);
        return asset;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var asset = await _context.MediaAssets.Include(m => m.Usages).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (asset is null) return;

        if (asset.Usages.Count > 0)
            throw new InvalidOperationException($"Cannot delete media asset {id}: it has {asset.Usages.Count} usage(s).");

        DeleteIfExists(asset.FilePath);

        if (!string.IsNullOrEmpty(asset.ThumbnailsJson))
        {
            try
            {
                var thumbs = JsonSerializer.Deserialize<Dictionary<string, string>>(asset.ThumbnailsJson);
                if (thumbs is not null)
                    foreach (var (_, path) in thumbs) DeleteIfExists(path);
            }
            catch (JsonException)
            {
                _logger.LogWarning("MediaAsset {Id} has malformed ThumbnailsJson; skipping thumbnail cleanup.", id);
            }
        }

        _context.MediaAssets.Remove(asset);
        await _context.SaveChangesAsync(ct);
    }

    // ---------------- internals ----------------

    /// <summary>
    /// Writes the file to {wwwroot}/{MediaRoot}/{sourceFolder}/{hash[..2]}/{hash}.{ext}
    /// and returns the web-relative URL plus the absolute disk path.
    /// </summary>
    private async Task<(string relativeUrl, string absolutePath)> PersistToDiskAsync(
        string sourceFolder, string hash, byte[] bytes, string mimeType, CancellationToken ct)
    {
        var ext = ExtensionFor(mimeType);
        var shard = hash.Length >= 2 ? hash[..2] : "00";
        var folder = Path.Combine(_environment.WebRootPath, MediaRoot, sourceFolder, shard);
        Directory.CreateDirectory(folder);

        var fileName = $"{hash}.{ext}";
        var absolutePath = Path.Combine(folder, fileName);
        await File.WriteAllBytesAsync(absolutePath, bytes, ct);

        var relativeUrl = $"/{MediaRoot}/{sourceFolder}/{shard}/{fileName}";
        return (relativeUrl, absolutePath);
    }

    private async Task<Dictionary<string, string>> GenerateThumbnailsAsync(string hash, byte[] sourceBytes, CancellationToken ct)
    {
        var thumbs = new Dictionary<string, string>();
        var folder = Path.Combine(_environment.WebRootPath, MediaRoot, ThumbnailSubPath, hash.Length >= 2 ? hash[..2] : "00");
        Directory.CreateDirectory(folder);

        foreach (var (label, width, height) in new[]
        {
            ("thumb", 200, 200),
            ("web",   800, 600)
        })
        {
            try
            {
                var jpeg = _imaging.CreateThumbnailJpeg(sourceBytes, width, height);
                var fileName = $"{hash}_{label}_{width}x{height}.jpg";
                var absolutePath = Path.Combine(folder, fileName);
                await File.WriteAllBytesAsync(absolutePath, jpeg, ct);
                thumbs[label] = $"/{MediaRoot}/{ThumbnailSubPath}/{hash[..2]}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate {Label} thumbnail for hash {Hash}", label, hash);
            }
        }

        return thumbs;
    }

    private void DeleteIfExists(string webRelativePath)
    {
        if (string.IsNullOrEmpty(webRelativePath)) return;
        var absolute = Path.Combine(_environment.WebRootPath, webRelativePath.TrimStart('/'));
        try { if (File.Exists(absolute)) File.Delete(absolute); }
        catch (IOException ex) { _logger.LogWarning(ex, "Failed to delete media file {Path}", absolute); }
    }

    private static string ExtensionFor(string mimeType) => mimeType switch
    {
        "image/jpeg"     => "jpg",
        "image/png"      => "png",
        "image/webp"     => "webp",
        "image/gif"      => "gif",
        "image/svg+xml"  => "svg",
        _                => "bin"
    };
}
