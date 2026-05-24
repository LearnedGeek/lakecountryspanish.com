using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// The single textarea Karen fills in to describe the lesson she wants. The
/// optional UnitId and GradeBand let her override the drafter's inferences;
/// both default to "let the AI pick."
/// </summary>
public sealed class CurriculumDraftBriefViewModel
{
    [Required(ErrorMessage = "Tell us what lesson you'd like to draft.")]
    [Display(Name = "Describe your lesson")]
    [StringLength(4000, MinimumLength = 20, ErrorMessage = "Briefs should be at least 20 characters so the drafter has something to work with.")]
    public string Brief { get; set; } = string.Empty;

    [Display(Name = "Force a parent Unit (optional)")]
    public int? UnitId { get; set; }

    [Display(Name = "Force a grade band (optional)")]
    public GradeBand? GradeBand { get; set; }

    public IReadOnlyList<UnitOption> AvailableUnits { get; set; } = Array.Empty<UnitOption>();

    /// <summary>Set when the drafter is missing an API key.</summary>
    public bool DrafterAvailable { get; set; }

    /// <summary>Surfaced when a draft attempt fails.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Read-only review of a drafted (or hand-authored) Day. The body markdown is
/// already compiled, so the existing renderer pipeline produces the preview.
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
