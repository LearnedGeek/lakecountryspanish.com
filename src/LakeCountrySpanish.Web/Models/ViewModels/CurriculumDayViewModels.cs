using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

public sealed class CurriculumDayListItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string UnitTitle { get; init; } = string.Empty;
    public int DayNumberInUnit { get; init; }
    public GradeBand GradeBand { get; init; }
    public string Theme { get; init; } = string.Empty;
    public int EstimatedDurationMinutes { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastModifiedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class CurriculumDayListViewModel
{
    public IReadOnlyList<CurriculumDayListItemViewModel> Days { get; init; } = Array.Empty<CurriculumDayListItemViewModel>();
    public int? UnitFilter { get; init; }
    public GradeBand? GradeBandFilter { get; init; }
    public IReadOnlyList<UnitOption> AvailableUnits { get; init; } = Array.Empty<UnitOption>();
}

public sealed record UnitOption(int Id, string Title);

public sealed class CurriculumDayFormViewModel
{
    public int Id { get; set; }                          // 0 = create, >0 = edit

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [StringLength(800)]
    public string Description { get; set; } = string.Empty;

    [Required, Range(1, int.MaxValue, ErrorMessage = "Pick a parent Unit.")]
    [Display(Name = "Unit")]
    public int UnitId { get; set; }

    [Range(1, 99)]
    [Display(Name = "Day # in unit")]
    public int DayNumberInUnit { get; set; } = 1;

    [Display(Name = "Grade band")]
    public GradeBand GradeBand { get; set; } = GradeBand.K4;

    [StringLength(80)]
    public string Theme { get; set; } = string.Empty;

    [Display(Name = "Estimated duration (minutes)")]
    [Range(5, 240)]
    public int EstimatedDurationMinutes { get; set; } = 25;

    [Display(Name = "Skill focus")]
    public SkillFocus SkillFocus { get; set; } = SkillFocus.Mixed;

    /// <summary>
    /// Compiled Markdown — derived from the block editor on edit, NOT user-editable
    /// from the metadata form. Nullable so MVC's implicit-required-for-non-nullable
    /// rule doesn't block create with no body yet (blocks haven't been added).
    /// Block endpoints are the only path that writes lesson body content.
    /// </summary>
    public string? TeacherPlanMarkdown { get; set; }

    /// <summary>
    /// Structured block list as JSON. Authoritative content representation.
    /// Populated by the controller from the Day entity; the form passes it
    /// through but doesn't bind back into it (HTMX block endpoints write it).
    /// </summary>
    public string? BodyBlocksJson { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Change notes (optional)")]
    [StringLength(280)]
    public string? ChangeNotes { get; set; }

    // Populated by the controller on each render so the Unit <select> has options.
    public IReadOnlyList<UnitOption> AvailableUnits { get; set; } = Array.Empty<UnitOption>();

    public bool IsEdit => Id > 0;
    public string PageTitle => IsEdit ? "Edit Day" : "New Day";
}
