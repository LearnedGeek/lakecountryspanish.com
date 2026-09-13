using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services.Curriculum;

/// <summary>
/// CRUD for <see cref="CurriculumDocument"/> — teacher binders and future
/// artifact types. Files land in a config-driven directory outside
/// wwwroot so teacher-role auth is enforced by the controller action
/// rather than accidentally granted by static-file serving. See issue #20.
/// </summary>
public interface ICurriculumDocumentService
{
    /// <summary>All documents, ordered for the teacher list view (family then doctype).</summary>
    Task<IReadOnlyList<CurriculumDocument>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Documents attached to a specific curriculum family.</summary>
    Task<IReadOnlyList<CurriculumDocument>> ListByFamilyAsync(string curriculumFamily, CancellationToken ct = default);

    /// <summary>Fetch a single document with its grade bands loaded.</summary>
    Task<CurriculumDocument?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Full absolute path on disk for download / serve. Null if the document is missing.</summary>
    string GetAbsolutePath(CurriculumDocument document);

    /// <summary>
    /// Upload or replace-in-place. If a document with the same
    /// (family, docType) already exists, its file is replaced and the
    /// existing DB row is updated (UpdatedAt bumped, GradeBands
    /// reconciled) — no duplicate row is created.
    /// </summary>
    Task<CurriculumDocument> UploadAsync(
        string curriculumFamily,
        CurriculumDocumentType documentType,
        IReadOnlyList<GradeBand> gradeBands,
        string title,
        Stream fileStream,
        string originalFileName,
        long fileSizeBytes,
        string uploadedById,
        CancellationToken ct = default);

    /// <summary>Delete a document (row + file). Idempotent-ish; silently no-ops on missing.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}
