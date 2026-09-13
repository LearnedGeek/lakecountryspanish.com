using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Services.Curriculum;

public sealed class CurriculumDocumentService : ICurriculumDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CurriculumDocumentService> _logger;

    // Config-driven so prod can point at the persistent volume mount
    // (/var/lib/lakecountryspanish-data/binders/) via env override, while
    // dev falls back to a folder inside the app's content root. Kept OUT of
    // wwwroot so downloads go through the controller action (auth enforced)
    // rather than being served as static files.
    private const string StorageConfigKey = "Storage:BindersDir";

    public CurriculumDocumentService(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<CurriculumDocumentService> logger)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<IReadOnlyList<CurriculumDocument>> ListAllAsync(CancellationToken ct = default) =>
        LoadAsync(_ => true, ct);

    public Task<IReadOnlyList<CurriculumDocument>> ListByFamilyAsync(string curriculumFamily, CancellationToken ct = default)
    {
        var normalized = (curriculumFamily ?? string.Empty).Trim().ToLowerInvariant();
        return LoadAsync(d => d.CurriculumFamily == normalized, ct);
    }

    private async Task<IReadOnlyList<CurriculumDocument>> LoadAsync(
        System.Linq.Expressions.Expression<Func<CurriculumDocument, bool>> where,
        CancellationToken ct)
    {
        return await _context.CurriculumDocuments
            .AsNoTracking()
            .Include(d => d.GradeBands)
            .Where(where)
            .OrderBy(d => d.CurriculumFamily)
            .ThenBy(d => (int)d.DocumentType)
            .ToListAsync(ct);
    }

    public Task<CurriculumDocument?> GetAsync(int id, CancellationToken ct = default) =>
        _context.CurriculumDocuments
            .Include(d => d.GradeBands)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public string GetAbsolutePath(CurriculumDocument document) =>
        Path.Combine(ResolveStorageRoot(), document.FilePath);

    public async Task<CurriculumDocument> UploadAsync(
        string curriculumFamily,
        CurriculumDocumentType documentType,
        IReadOnlyList<GradeBand> gradeBands,
        string title,
        Stream fileStream,
        string originalFileName,
        long fileSizeBytes,
        string uploadedById,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(curriculumFamily))
            throw new ArgumentException("Curriculum family is required.", nameof(curriculumFamily));

        var normalizedFamily = curriculumFamily.Trim().ToLowerInvariant();
        var storageRoot = ResolveStorageRoot();
        Directory.CreateDirectory(Path.Combine(storageRoot, normalizedFamily));

        // Filename: {doctype}-{yyyyMMddHHmmss}.pdf under the family folder.
        // Timestamp in the name means each upload is a fresh file — the
        // prior file is deleted after the DB row is updated so a partial
        // failure doesn't corrupt the current binder.
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(ext)) ext = ".pdf";
        var relativePath = Path.Combine(normalizedFamily, $"{documentType.ToString().ToLowerInvariant()}-{stamp}{ext}").Replace('\\', '/');
        var absolutePath = Path.Combine(storageRoot, relativePath);

        // Write the file first (before touching the DB) so a DB failure
        // leaves an orphan file rather than a broken DB row pointing at
        // nothing. Orphans are noisy but recoverable; a broken DB row
        // means the app renders "download this" and 500s on click.
        await using (var fs = File.Create(absolutePath))
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        // Look up existing (family, docType) — replace-in-place semantics.
        var existing = await _context.CurriculumDocuments
            .Include(d => d.GradeBands)
            .FirstOrDefaultAsync(d => d.CurriculumFamily == normalizedFamily && d.DocumentType == documentType, ct);

        CurriculumDocument doc;
        string? priorAbsolutePath = null;
        if (existing is not null)
        {
            priorAbsolutePath = Path.Combine(storageRoot, existing.FilePath);
            existing.FilePath = relativePath;
            existing.Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(originalFileName) : title.Trim();
            existing.OriginalFileName = originalFileName;
            existing.FileSizeBytes = fileSizeBytes;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UploadedById = uploadedById;
            existing.GradeBands.Clear();
            foreach (var band in gradeBands.Distinct())
            {
                existing.GradeBands.Add(new CurriculumDocumentGradeBand { GradeBand = band });
            }
            doc = existing;
        }
        else
        {
            doc = new CurriculumDocument
            {
                CurriculumFamily = normalizedFamily,
                DocumentType = documentType,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(originalFileName) : title.Trim(),
                FilePath = relativePath,
                OriginalFileName = originalFileName,
                FileSizeBytes = fileSizeBytes,
                UploadedAt = DateTime.UtcNow,
                UploadedById = uploadedById
            };
            foreach (var band in gradeBands.Distinct())
            {
                doc.GradeBands.Add(new CurriculumDocumentGradeBand { GradeBand = band });
            }
            _context.CurriculumDocuments.Add(doc);
        }

        await _context.SaveChangesAsync(ct);

        // Prior file cleanup after DB commit — if this fails we've orphaned
        // it, but the current row + file are consistent. Best-effort.
        if (priorAbsolutePath is not null && File.Exists(priorAbsolutePath) && priorAbsolutePath != absolutePath)
        {
            try { File.Delete(priorAbsolutePath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete prior binder file {Path}", priorAbsolutePath); }
        }

        _logger.LogInformation(
            "Uploaded curriculum document {Id} ({Family}/{DocType}) — {Bytes} bytes",
            doc.Id, doc.CurriculumFamily, doc.DocumentType, doc.FileSizeBytes);
        return doc;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var doc = await _context.CurriculumDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return;

        var absolutePath = Path.Combine(ResolveStorageRoot(), doc.FilePath);

        _context.CurriculumDocuments.Remove(doc);
        await _context.SaveChangesAsync(ct);

        // Delete file after row is gone so a partial failure leaves an
        // orphan file rather than a live row pointing at a missing file.
        if (File.Exists(absolutePath))
        {
            try { File.Delete(absolutePath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete binder file {Path}", absolutePath); }
        }

        _logger.LogInformation("Deleted curriculum document {Id} ({Family}/{DocType})", doc.Id, doc.CurriculumFamily, doc.DocumentType);
    }

    /// <summary>
    /// Resolves the on-disk root where binder PDFs live. Prefers the
    /// configured <c>Storage:BindersDir</c> (set in appsettings or overridden
    /// via env var in prod). Falls back to <c>{contentRoot}/UploadedBinders</c>
    /// for local dev. Always ensures the directory exists.
    /// </summary>
    private string ResolveStorageRoot()
    {
        var configured = _configuration[StorageConfigKey];
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(_environment.ContentRootPath, "UploadedBinders")
            : configured;

        Directory.CreateDirectory(root);
        return root;
    }
}
