namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Snapshot of a Day or ArtifactLibrary body at a point in time. Karen edits
/// the master content; each save creates a new CurriculumVersion record so
/// that a binder generated for a teacher in Period X can be reconstructed
/// exactly even after the master content has evolved.
///
/// Polymorphic over Day and ArtifactLibrary via <see cref="EntityType"/> +
/// <see cref="EntityId"/> — same pattern as <see cref="MediaUsage"/>. Avoids
/// a separate version table per content type while keeping the audit trail
/// in one place.
/// </summary>
public class CurriculumVersion
{
    public int Id { get; set; }

    /// <summary>
    /// Type of versioned entity. Currently "Day" or "ArtifactLibrary".
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Primary key of the versioned entity.
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Monotonically increasing version number per entity. v1 is the initial
    /// creation; v2, v3, ... are subsequent edits.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Snapshot of the entity's main Markdown body at this version. For a
    /// Day this is the TeacherPlanMarkdown; for an ArtifactLibrary item this
    /// is the BodyMarkdown.
    /// </summary>
    public string BodySnapshotMarkdown { get; set; } = string.Empty;

    /// <summary>
    /// When this version became effective (i.e. when Karen saved it).
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// FK to <see cref="ApplicationUser"/> who created this version.
    /// </summary>
    public string ChangedById { get; set; } = string.Empty;

    /// <summary>
    /// Optional change notes / commit message.
    /// </summary>
    public string? ChangeNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
