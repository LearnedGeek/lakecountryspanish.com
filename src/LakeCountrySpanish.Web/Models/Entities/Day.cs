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
    /// Compiled Markdown body — the teacher-facing lesson plan rendered by
    /// the binder generator. Uses LCS custom container blocks (:::vocab,
    /// :::warmup, :::teach, :::practice, :::game, :::worksheet,
    /// :::cultural-note, :::teacher-note, etc.).
    ///
    /// For Days authored through the block editor this field is a *cache* —
    /// the source of truth is <see cref="BodyBlocksJson"/>, and the server
    /// recompiles this column on every save via <c>IBlockCompiler</c>. The
    /// downstream rendering pipeline reads this column unchanged.
    /// </summary>
    public string TeacherPlanMarkdown { get; set; } = string.Empty;

    /// <summary>
    /// Structured block list as JSON — the authoritative representation for
    /// content authored in the visual block editor. Each block has a
    /// discriminated <c>type</c> (paragraph, vocab, warmup, bingo-card, etc.)
    /// and type-specific fields. When this column is non-empty, it owns the
    /// content and <see cref="TeacherPlanMarkdown"/> is a compiled cache.
    /// Empty for legacy Days authored before the block editor shipped.
    /// </summary>
    public string BodyBlocksJson { get; set; } = string.Empty;

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

    /// <summary>
    /// FK to ApplicationUser who last touched this Day (any field). Distinct
    /// from CreatedById (immutable) and ReviewedById (only stamped on the
    /// activation/approval event). Closes the audit-trail gap for metadata-
    /// only edits and IsActive toggles, which don't produce a body-diff
    /// CurriculumVersion row.
    /// </summary>
    public string? LastModifiedById { get; set; }

    /// <summary>
    /// URL-safe slug used as the public route segment (`/curriculum/{slug}`).
    /// Derived from Title at creation; admin-editable. Unique across Days.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Number of class sessions this Day's content typically spans. Karen's
    /// docs note things like "se realizará en 2 a 3 clases"; we round to a
    /// single integer (the usual). Defaults to 1.
    /// </summary>
    public int Sessions { get; set; } = 1;

    /// <summary>
    /// JSON blob holding parsed lesson metadata that doesn't warrant its own
    /// column: vocab tiers (core/stretch/challenge arrays), materials list,
    /// cultura note, sessions count, standards alignment table. Populated by
    /// the docx parser; reshaped lightly by the admin reviewer. Schema is
    /// open by design — extending the docx template doesn't require a
    /// schema migration.
    /// </summary>
    public string MetadataJson { get; set; } = string.Empty;

    /// <summary>
    /// When true, this Day is reachable at `/curriculum/{Slug}` for any
    /// logged-in Student. When false (the default for drafts), the Day is
    /// admin-visible only.
    /// </summary>
    public bool IsPublic { get; set; } = false;

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

    /// <summary>
    /// Videos referenced by this Day's lesson (opening song, presentation song,
    /// closing song, supplemental). Parsed from the docx Songs table at
    /// upload time; each video gets its own shortlink-able URL.
    /// </summary>
    public virtual ICollection<LessonVideo> Videos { get; set; } = new List<LessonVideo>();
}
