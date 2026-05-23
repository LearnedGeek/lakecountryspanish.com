namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Audit log of a binder render. Created at the moment a teacher generates
/// their binder HTML (browser-print path) or PDF (Phase 4 server-side path).
/// Captures which versions of which content were included so a teacher can
/// later be told "you printed v3 of the Colors lesson" or LCS can verify
/// that distributed materials were the version current at print time.
/// </summary>
public class BinderGeneration
{
    public int Id { get; set; }

    public int TeacherClassAssignmentId { get; set; }
    public virtual TeacherClassAssignment TeacherClassAssignment { get; set; } = null!;

    /// <summary>
    /// Optional FK to the BinderComposition this generation rendered from.
    /// Null if the teacher rendered an ad-hoc selection without saving a
    /// composition.
    /// </summary>
    public int? BinderCompositionId { get; set; }
    public virtual BinderComposition? BinderComposition { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON array of Day IDs included in the generated binder.
    /// </summary>
    public string DayIdsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of ArtifactLibrary IDs included.
    /// </summary>
    public string ArtifactIdsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of CurriculumVersion IDs captured at generation time —
    /// so the exact content state at render is reconstructable.
    /// </summary>
    public string CurriculumVersionIdsJson { get; set; } = "[]";

    /// <summary>
    /// Computed watermark text rendered onto every binder page, e.g.
    /// "Property of Lake Country Spanish — Licensed to Cece TestTeacher for
    /// Fall 2026 — Not for redistribution".
    /// </summary>
    public string WatermarkText { get; set; } = string.Empty;
}
