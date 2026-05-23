namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Top-level type of an artifact in the LCS curriculum library. Each Day
/// composes references to multiple ArtifactLibrary items; teachers can also
/// pull standalone artifacts into a BinderComposition independently.
/// </summary>
public enum ArtifactType
{
    Newsletter = 0,
    Flashcard = 1,
    CulturalImageSet = 2,
    Game = 3,
    Worksheet = 4,
    Song = 5,
    StoryReader = 6,
    VocabList = 7,
    Craft = 8,
    ReadingPassage = 9,
    Award = 10,
    Template = 11,
    Other = 99
}

/// <summary>
/// Relative difficulty of an artifact within its grade band. Used to
/// surface easier vs. harder variants when a teacher is choosing between
/// multiple artifacts on the same theme.
/// </summary>
public enum ArtifactDifficulty
{
    Easy = 0,
    Moderate = 1,
    Challenging = 2
}

/// <summary>
/// A first-class, reusable artifact in the LCS curriculum library
/// (worksheets, games, flashcards, newsletters, etc.). Artifacts are NOT
/// bound to a single Day — they're cataloged independently and referenced
/// by Days for "recommended" use, while teachers can also pull them ad-hoc
/// into BinderCompositions.
///
/// The Type / Subtype split keeps the high-level category as an enum (for
/// indexing and filtering) while leaving Subtype as a free-form string for
/// flexibility (new game styles, new worksheet formats — no schema change).
/// </summary>
public class ArtifactLibrary
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type enum (Worksheet, Game, Flashcard, etc.).
    /// </summary>
    public ArtifactType Type { get; set; }

    /// <summary>
    /// Free-form subtype string for finer-grained classification, e.g.
    /// "Game-Bingo", "Game-Concentration", "Worksheet-Trace",
    /// "Worksheet-ColorByNumber", "Flashcard-Vocab", "Craft-Wheel".
    /// </summary>
    public string? Subtype { get; set; }

    /// <summary>
    /// URL-safe slug — used to reference this artifact from a Day's Markdown
    /// (e.g. <c>:::game-ref slug="bingo-colores"</c>) and as part of file
    /// paths for any associated assets.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Skill focus tag (Listening / Reading / Speaking / Writing / Mixed).
    /// </summary>
    public SkillFocus SkillFocus { get; set; } = SkillFocus.Mixed;

    public ArtifactDifficulty Difficulty { get; set; } = ArtifactDifficulty.Moderate;

    /// <summary>
    /// Markdown body — the printable content of this artifact, rendered by
    /// the binder generator using the appropriate CSS variant (worksheet-k2,
    /// game-card, flashcard-grid, etc.).
    /// </summary>
    public string BodyMarkdown { get; set; } = string.Empty;

    /// <summary>
    /// Which CSS theme variant to use when rendering this artifact, e.g.
    /// "worksheet-k2", "game-card", "flashcard-grid", "newsletter".
    /// </summary>
    public string? PrintCssVariant { get; set; }

    /// <summary>
    /// Optional path to a static asset for artifacts that aren't Markdown-
    /// rendered (e.g. a pre-existing PDF, an SVG cut-out template).
    /// </summary>
    public string? AssetFilePath { get; set; }

    /// <summary>
    /// Comma-separated thematic tags (e.g. "colors, family, food"). Used
    /// for cross-cutting search; complements the Type enum.
    /// </summary>
    public string? Themes { get; set; }

    // Review workflow — mirrors Assignment review pattern
    public string? CreatedById { get; set; }
    public string? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// WI standards this artifact supports.
    /// </summary>
    public virtual ICollection<WisconsinStandard> WisconsinStandards { get; set; } = new List<WisconsinStandard>();

    /// <summary>
    /// Days that recommend this artifact.
    /// </summary>
    public virtual ICollection<Day> RecommendingDays { get; set; } = new List<Day>();

    /// <summary>
    /// Version history of this artifact's content.
    /// </summary>
    public virtual ICollection<CurriculumVersion> Versions { get; set; } = new List<CurriculumVersion>();
}
