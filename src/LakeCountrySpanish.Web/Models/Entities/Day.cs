namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// A single teaching session — Karen's mental unit of curriculum, matching
/// her existing 8-Day unit structure from her years at Futura. A Day has a
/// teacher-facing lesson plan (Markdown body), recommended artifacts
/// (worksheets, games, flashcards), tagged WI standards, and optional
/// linked Assignments for between-class practice.
/// </summary>
public class Day
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// FK to the parent Unit.
    /// </summary>
    public int UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;

    /// <summary>
    /// Day number within the parent Unit (1..N). Karen's existing units have
    /// 8 days each.
    /// </summary>
    public int DayNumberInUnit { get; set; }

    /// <summary>
    /// Grade this Day is appropriate for. Inherits from the parent Unit's
    /// LearningPath but stored explicitly for efficient filtering.
    /// </summary>
    public GradeBand GradeBand { get; set; }

    /// <summary>
    /// Thematic label (e.g. "Colors"). Inherits from parent Unit but stored
    /// explicitly for cross-Unit searches by theme.
    /// </summary>
    public string Theme { get; set; } = string.Empty;

    /// <summary>
    /// Markdown body — the teacher-facing lesson plan rendered by the binder
    /// generator. Uses LCS custom container blocks (:::vocab, :::warmup,
    /// :::teach, :::practice, :::game, :::worksheet, :::cultural-note,
    /// :::teacher-note, etc.).
    /// </summary>
    public string TeacherPlanMarkdown { get; set; } = string.Empty;

    public int EstimatedDurationMinutes { get; set; } = 25;

    /// <summary>
    /// Optional pedagogical skill focus tag, independent of WI standards.
    /// </summary>
    public SkillFocus SkillFocus { get; set; } = SkillFocus.Mixed;

    // Review workflow — mirrors the existing Assignment review pattern
    /// <summary>
    /// FK to ApplicationUser who authored this Day.
    /// </summary>
    public string? CreatedById { get; set; }

    /// <summary>
    /// FK to ApplicationUser who reviewed and approved this Day, if any.
    /// </summary>
    public string? ReviewedById { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// WI standards this Day teaches against. Many-to-many: a Day can target
    /// multiple standards and a standard can be targeted by many Days.
    /// </summary>
    public virtual ICollection<WisconsinStandard> WisconsinStandards { get; set; } = new List<WisconsinStandard>();

    /// <summary>
    /// Recommended ArtifactLibrary items for this Day (worksheets, games,
    /// flashcards). Recommendations, not bindings — teachers compose final
    /// binders ad-hoc via BinderComposition.
    /// </summary>
    public virtual ICollection<ArtifactLibrary> RecommendedArtifacts { get; set; } = new List<ArtifactLibrary>();

    /// <summary>
    /// Optional structured practice items (existing Assignment system).
    /// </summary>
    public virtual ICollection<Assignment> PracticeAssignments { get; set; } = new List<Assignment>();

    /// <summary>
    /// Version history of this Day's content. Karen edits the Day;
    /// CurriculumVersion records snapshot the body at each save.
    /// </summary>
    public virtual ICollection<CurriculumVersion> Versions { get; set; } = new List<CurriculumVersion>();
}
