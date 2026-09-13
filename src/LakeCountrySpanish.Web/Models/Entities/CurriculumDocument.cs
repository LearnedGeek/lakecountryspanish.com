namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// A teacher-facing artifact (PDF) attached to a curriculum concept —
/// e.g. the K4-K5 teacher binder for Bailamos y Aprendemos.
///
/// Deliberately attached to a <see cref="CurriculumFamily"/> string
/// rather than a specific <see cref="EnrollmentProgram"/> row so Karen
/// doesn't have to re-upload the same PDF every time she runs Bailamos
/// K-2 at a new school. The (CurriculumFamily × GradeBands ×
/// DocumentType) tuple identifies "the binder" that every matching
/// program instance shares.
///
/// Uploaded and deleted by Admins only (Karen + Cece). Viewable by
/// Teacher + Admin roles. See issue #20 for the full design.
/// </summary>
public class CurriculumDocument
{
    public int Id { get; set; }

    /// <summary>
    /// Canonical family identifier — matches
    /// <see cref="EnrollmentProgram.CurriculumFamily"/>. Free-form-ish
    /// (Karen picks via creatable dropdown), but should be lowercase +
    /// hyphenated by convention: "bailamos", "beginner-spanish", etc.
    /// </summary>
    public string CurriculumFamily { get; set; } = string.Empty;

    /// <summary>
    /// Type of artifact — starts as <see cref="CurriculumDocumentType.TeacherBinder"/>
    /// only, extensible without schema change.
    /// </summary>
    public CurriculumDocumentType DocumentType { get; set; } = CurriculumDocumentType.TeacherBinder;

    /// <summary>Display title (defaults to the uploaded filename minus extension).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Relative path under the persistent uploads directory
    /// (<c>/var/lib/lakecountryspanish-data/binders/</c>). Streamed by the
    /// controller — never served as a static file — so Teacher role auth
    /// is enforced on every download.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Original filename from the uploader — useful for the Content-Disposition download header.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Byte size of the file at upload time — useful for the list view.</summary>
    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedById { get; set; } = string.Empty;

    /// <summary>Timestamp of the most recent replace-in-place update; null if never replaced.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Grade bands this document covers. A single binder can span multiple
    /// bands (K-2 = K4+K5+Grade1+Grade2 with one row instead of four).
    /// Cascade-deleted with the document.
    /// </summary>
    public virtual ICollection<CurriculumDocumentGradeBand> GradeBands { get; set; } = new List<CurriculumDocumentGradeBand>();
}
