namespace LakeCountrySpanish.Web.Services.Media;

/// <summary>
/// Orchestrates AI image generation for vocab terms whose images haven't
/// been created yet. Idempotent — re-running for a theme only generates
/// images for terms with NULL MediaAssetId.
/// </summary>
public interface IVocabImageGenerationService
{
    /// <summary>
    /// Generates images for every term in the given theme that doesn't yet
    /// have one. Returns a per-run summary so the caller (admin UI) can show
    /// the user what happened.
    /// </summary>
    Task<VocabGenerationSummary> GenerateMissingForThemeAsync(
        string theme,
        string triggeredByUserId,
        CancellationToken ct = default);
}

public sealed record VocabGenerationSummary(
    string Theme,
    int Considered,
    int AlreadyHadImage,
    int Generated,
    int Failed,
    IReadOnlyList<VocabGenerationFailure> Failures);

public sealed record VocabGenerationFailure(string SpanishTerm, string ErrorMessage);
