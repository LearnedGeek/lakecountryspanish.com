using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Services.Media;

public sealed class VocabImageGenerationService : IVocabImageGenerationService
{
    /// <summary>
    /// Base prompt template. The {0} is the English noun. Aims for the
    /// flat-cartoon-clipart aesthetic Karen showed in IMG_8908 — single
    /// isolated object, white background, bold outlines, no text.
    /// Iterate this template if Karen wants a different look.
    /// </summary>
    private const string PromptTemplate =
        "flat children's-book clipart of a {0} on a plain white background, " +
        "simple cartoon style, brightly colored, bold outlines, " +
        "single isolated object centered in frame, " +
        "no text, no shadows, no patterns, no border";

    private readonly ApplicationDbContext _context;
    private readonly IMediaService _media;
    private readonly IAiImageService _ai;
    private readonly ILogger<VocabImageGenerationService> _logger;

    public VocabImageGenerationService(
        ApplicationDbContext context,
        IMediaService media,
        IAiImageService ai,
        ILogger<VocabImageGenerationService> logger)
    {
        _context = context;
        _media = media;
        _ai = ai;
        _logger = logger;
    }

    public async Task<VocabGenerationSummary> GenerateMissingForThemeAsync(
        string theme,
        string triggeredByUserId,
        CancellationToken ct = default)
    {
        if (!_ai.IsAvailable)
            throw new InvalidOperationException(
                $"AI image service '{_ai.ProviderName}' is not configured. " +
                $"Add the API token to appsettings.Local.json.");

        var allInTheme = await _context.VocabTerms
            .Where(v => v.Theme == theme)
            .OrderBy(v => v.Spanish)
            .ToListAsync(ct);

        var missing = allInTheme.Where(v => v.MediaAssetId is null).ToList();
        var failures = new List<VocabGenerationFailure>();
        var generated = 0;

        foreach (var term in missing)
        {
            ct.ThrowIfCancellationRequested();

            var prompt = string.Format(PromptTemplate, term.English);
            try
            {
                _logger.LogInformation(
                    "Generating image for vocab term {Article} {Spanish} ({English})",
                    term.Article, term.Spanish, term.English);

                var asset = await _media.ImportFromAiAsync(
                    _ai,
                    prompt,
                    new ImportMediaOptions
                    {
                        Category = MediaCategory.FlashcardArt,
                        Title = $"{term.Article} {term.Spanish}",
                        AltText = $"Illustration of {term.English}",
                        Tags = $"vocab, {term.Theme}, {term.English}",
                        ImportedById = triggeredByUserId
                    },
                    ct);

                term.MediaAssetId = asset.Id;
                await _context.SaveChangesAsync(ct);
                generated++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate image for vocab term {Spanish}",
                    term.Spanish);
                failures.Add(new VocabGenerationFailure(term.Spanish, ex.Message));
            }
        }

        return new VocabGenerationSummary(
            Theme: theme,
            Considered: allInTheme.Count,
            AlreadyHadImage: allInTheme.Count - missing.Count,
            Generated: generated,
            Failed: failures.Count,
            Failures: failures);
    }
}
