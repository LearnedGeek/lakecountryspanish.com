using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.Extensions.Options;

namespace LakeCountrySpanish.Web.Services.Media;

/// <summary>
/// Pixabay search/import adapter. Pixabay's free tier allows 5000 requests
/// per hour and exposes both photos and vector illustrations — strong fit
/// for K4-2 worksheet artwork that needs cartoon clip art, not just photos.
///
/// Attribution per Pixabay TOS: photographer credit + source link on
/// downstream pages where the image is displayed (handled by the binder
/// attribution-page renderer).
/// </summary>
public sealed class PixabayImageSourceAdapter : IImageSourceAdapter
{
    private const string ApiBase = "https://pixabay.com/api/";

    private readonly HttpClient _http;
    private readonly PixabaySettings _settings;
    private readonly ILogger<PixabayImageSourceAdapter> _logger;

    public PixabayImageSourceAdapter(
        HttpClient http,
        IOptions<PixabaySettings> settings,
        ILogger<PixabayImageSourceAdapter> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public string SourceName => "Pixabay";
    public MediaSource SourceEnum => MediaSource.Pixabay;
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public async Task<ImageSearchResults> SearchAsync(ImageSearchQuery query, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Pixabay API key is not configured (Pixabay:ApiKey).");

        var imageType = query.ContentType switch
        {
            ImageContentType.Photo        => "photo",
            ImageContentType.Illustration => "illustration",
            ImageContentType.Vector       => "vector",
            _                             => "all"
        };

        // Pixabay caps per_page between 3 and 200 and page between 1 and 500/perPage.
        var perPage = Math.Clamp(query.PerPage, 3, 200);
        var page = Math.Max(1, query.Page);

        var url = $"{ApiBase}?key={Uri.EscapeDataString(_settings.ApiKey)}"
                + $"&q={Uri.EscapeDataString(query.Query)}"
                + $"&image_type={imageType}"
                + $"&per_page={perPage}&page={page}"
                + "&safesearch=true";

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PixabaySearchResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Pixabay returned empty response.");

        var hits = payload.Hits.Select(h => new ImageSearchHit(
            SourceId: h.Id.ToString(),
            PageUrl: h.PageURL ?? string.Empty,
            PreviewUrl: h.PreviewURL ?? string.Empty,
            WebUrl: h.WebformatURL ?? string.Empty,
            FullUrl: h.LargeImageURL ?? h.WebformatURL ?? string.Empty,
            Width: h.ImageWidth,
            Height: h.ImageHeight,
            Photographer: h.User ?? "Unknown",
            PhotographerUrl: !string.IsNullOrEmpty(h.User)
                ? $"https://pixabay.com/users/{Uri.EscapeDataString(h.User)}-{h.UserId}/"
                : string.Empty,
            LicenseType: "Pixabay License",
            Tags: string.IsNullOrWhiteSpace(h.Tags)
                ? Array.Empty<string>()
                : h.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        ).ToList();

        return new ImageSearchResults(payload.Total, payload.TotalHits, page, hits);
    }

    public async Task<DownloadedImage> DownloadAsync(ImageSearchHit hit, CancellationToken ct = default)
    {
        // The largeImageURL from Pixabay sometimes requires authentication for
        // truly large originals; webformatURL (max 1280px on long edge) is the
        // documented "free embed" URL and is plenty for K4-2 worksheet print.
        var url = !string.IsNullOrEmpty(hit.WebUrl) ? hit.WebUrl : hit.FullUrl;
        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException($"No downloadable URL for Pixabay hit {hit.SourceId}.");

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var mime = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

        var ext = mime switch
        {
            "image/jpeg" => "jpg",
            "image/png"  => "png",
            "image/webp" => "webp",
            "image/svg+xml" => "svg",
            _            => "bin"
        };
        var filename = $"pixabay-{hit.SourceId}.{ext}";

        return new DownloadedImage(bytes, mime, filename);
    }

    // -------- Pixabay API JSON shape --------

    private sealed class PixabaySearchResponse
    {
        [JsonPropertyName("total")]    public int Total { get; set; }
        [JsonPropertyName("totalHits")] public int TotalHits { get; set; }
        [JsonPropertyName("hits")]     public List<PixabayHit> Hits { get; set; } = new();
    }

    private sealed class PixabayHit
    {
        [JsonPropertyName("id")]            public long Id { get; set; }
        [JsonPropertyName("pageURL")]       public string? PageURL { get; set; }
        [JsonPropertyName("type")]          public string? Type { get; set; }
        [JsonPropertyName("tags")]          public string? Tags { get; set; }
        [JsonPropertyName("previewURL")]    public string? PreviewURL { get; set; }
        [JsonPropertyName("webformatURL")]  public string? WebformatURL { get; set; }
        [JsonPropertyName("largeImageURL")] public string? LargeImageURL { get; set; }
        [JsonPropertyName("imageWidth")]    public int ImageWidth { get; set; }
        [JsonPropertyName("imageHeight")]   public int ImageHeight { get; set; }
        [JsonPropertyName("user")]          public string? User { get; set; }
        [JsonPropertyName("user_id")]       public long UserId { get; set; }
    }
}
