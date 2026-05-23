namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// How a media asset is used inside a referencing entity. Helps the binder
/// renderer decide layout (cover vs inline vs flashcard-side vs bingo-cell).
/// </summary>
public enum MediaUsageType
{
    /// <summary>Cover image for the parent artifact (Day, Worksheet, etc.).</summary>
    Featured = 0,
    /// <summary>Embedded inline in the body content.</summary>
    Inline = 1,
    /// <summary>Front side of a flashcard.</summary>
    FlashcardFront = 2,
    /// <summary>Back side of a flashcard.</summary>
    FlashcardBack = 3,
    /// <summary>Cell illustration in a bingo card.</summary>
    BingoCell = 4,
    /// <summary>Page background.</summary>
    Background = 5,
    /// <summary>Mascot illustration on the page.</summary>
    Mascot = 6
}

/// <summary>
/// Tracks where a <see cref="MediaAsset"/> is used. Polymorphic on
/// <see cref="EntityType"/> + <see cref="EntityId"/> rather than a hard FK
/// because media is referenced by multiple entity types (Day,
/// ArtifactLibrary, Unit, LearningPath) and we want a single usage table.
///
/// Used to:
/// - Prevent orphan deletes (warn before deleting media that's still referenced)
/// - Find unused assets (storage cleanup)
/// - Build the attribution page in printed binders (group photographers per
///   binder by walking MediaUsage from each included Day / Artifact).
/// </summary>
public class MediaUsage
{
    public int Id { get; set; }

    public int MediaAssetId { get; set; }
    public virtual MediaAsset MediaAsset { get; set; } = null!;

    /// <summary>
    /// Type of consuming entity. Stored as string for flexibility — e.g.
    /// "Day", "ArtifactLibrary", "Unit", "LearningPath".
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Primary key of the consuming entity (matching <see cref="EntityType"/>).
    /// </summary>
    public int EntityId { get; set; }

    public MediaUsageType UsageType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
