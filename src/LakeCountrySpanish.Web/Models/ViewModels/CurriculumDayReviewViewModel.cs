using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// Read-only review of a Day. The body markdown is already compiled,
/// so the existing renderer pipeline produces the preview.
/// </summary>
public sealed class CurriculumDayReviewViewModel
{
    public int DayId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Theme { get; init; } = string.Empty;
    public string UnitTitle { get; init; } = string.Empty;
    public GradeBand GradeBand { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> WiStandardCodes { get; init; } = Array.Empty<string>();

    /// <summary>Rendered HTML body, ready to drop into the lcs-document container.</summary>
    public string RenderedHtml { get; init; } = string.Empty;

    public string ThemeName { get; init; } = "lcs-k2";
}
