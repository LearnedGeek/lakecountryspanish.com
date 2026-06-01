using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LakeCountrySpanish.Web.Services.Media;

/// <summary>
/// Replicate image generation via the synchronous Prefer: wait endpoint.
/// One HTTP call returns the prediction with output URLs (no polling loop
/// needed for fast models like Flux Schnell, which usually completes in
/// 2-3 seconds).
///
/// API reference: https://replicate.com/docs/reference/http#predictions.create
/// Flux Schnell: https://replicate.com/black-forest-labs/flux-schnell
/// </summary>
public sealed class ReplicateAiImageService : IAiImageService
{
    private const string ApiBase = "https://api.replicate.com/v1";

    private readonly HttpClient _http;
    private readonly ReplicateSettings _settings;
    private readonly ILogger<ReplicateAiImageService> _logger;

    public ReplicateAiImageService(
        HttpClient http,
        IOptions<ReplicateSettings> settings,
        ILogger<ReplicateAiImageService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;

        if (IsAvailable)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
            // Prefer: wait makes the predictions endpoint block until done
            // (up to 60s). Saves us a polling loop for fast models.
            _http.DefaultRequestHeaders.Add("Prefer", "wait");
        }
    }

    public string ProviderName => "Replicate";
    public string ModelId => _settings.Model;
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_settings.ApiToken);

    public async Task<DownloadedImage> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Replicate API token is not configured (Replicate:ApiToken).");

        var url = $"{ApiBase}/models/{_settings.Model}/predictions";

        // Flux Schnell input shape. Other models would need a different shape;
        // when we add a second model we can move this to a per-model builder.
        var request = new ReplicateCreateRequest
        {
            Input = new ReplicateInput
            {
                Prompt = prompt,
                AspectRatio = "1:1",
                OutputFormat = "png",
                OutputQuality = 90,
                NumOutputs = 1,
                Megapixels = "1"
            }
        };

        var response = await _http.PostAsJsonAsync(url, request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Replicate generation failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Replicate returned {(int)response.StatusCode}: {body}");
        }

        var prediction = await response.Content.ReadFromJsonAsync<ReplicatePrediction>(cancellationToken: ct)
                         ?? throw new InvalidOperationException("Replicate returned empty response.");

        if (prediction.Status != "succeeded")
            throw new InvalidOperationException($"Replicate prediction did not succeed (status: {prediction.Status}, error: {prediction.Error}).");

        var imageUrl = prediction.Output?.FirstOrDefault()
                       ?? throw new InvalidOperationException("Replicate prediction had no output URLs.");

        // Download the generated image.
        var imgResponse = await _http.GetAsync(imageUrl, ct);
        imgResponse.EnsureSuccessStatusCode();
        var bytes = await imgResponse.Content.ReadAsByteArrayAsync(ct);
        var mime = imgResponse.Content.Headers.ContentType?.MediaType ?? "image/png";
        var filename = $"replicate-{prediction.Id}.png";

        return new DownloadedImage(bytes, mime, filename);
    }

    // -------- Replicate API JSON shapes --------

    private sealed class ReplicateCreateRequest
    {
        [JsonPropertyName("input")] public ReplicateInput Input { get; set; } = new();
    }

    private sealed class ReplicateInput
    {
        [JsonPropertyName("prompt")]         public string Prompt { get; set; } = string.Empty;
        [JsonPropertyName("aspect_ratio")]   public string AspectRatio { get; set; } = "1:1";
        [JsonPropertyName("output_format")]  public string OutputFormat { get; set; } = "png";
        [JsonPropertyName("output_quality")] public int OutputQuality { get; set; } = 90;
        [JsonPropertyName("num_outputs")]    public int NumOutputs { get; set; } = 1;
        [JsonPropertyName("megapixels")]     public string Megapixels { get; set; } = "1";
    }

    private sealed class ReplicatePrediction
    {
        [JsonPropertyName("id")]     public string Id { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("output")] public List<string>? Output { get; set; }
        [JsonPropertyName("error")]  public string? Error { get; set; }
    }
}
