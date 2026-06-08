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

    /// <summary>Legacy plain-Markdown render — kept only for diagnostics.</summary>
    public string RenderedHtml { get; init; } = string.Empty;

    public string ThemeName { get; init; } = "teacher-binder";

    /// <summary>
    /// Structured proposal-aesthetic print model. The Review page renders
    /// from this; the lighter <see cref="RenderedHtml"/> is unused.
    /// </summary>
    public LessonPrintViewModel Print { get; init; } = new();

    /// <summary>
    /// 3-character shortlink code for this lesson, e.g. "XFA". Renders as
    /// "lcs/XFA" in the sidebar. Null if no shortlink has been allocated.
    /// </summary>
    public string? ShortlinkCode { get; init; }
}
