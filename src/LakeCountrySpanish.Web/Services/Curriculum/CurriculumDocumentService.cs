using System.Text.RegularExpressions;
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

    // Slug pattern for a curriculum-family folder name. Constrained so any
    // future compromise of the admin form still cannot escape the storage
    // root: lowercase letter first, then letters / digits / hyphens only,
    // no dots, no separators, capped at 80 chars. Enforced at both the
    // ViewModel layer (BinderUploadViewModel.Validate) and here as a
    // defence-in-depth check before any Path.Combine.
    private static readonly Regex FamilySlugPattern =
        new(@"^[a-z][a-z0-9-]{0,79}$", RegexOptions.Compiled);

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
        ResolveWithinRoot(document.FilePath);

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
        var normalizedFamily = (curriculumFamily ?? string.Empty).Trim().ToLowerInvariant();
        if (!FamilySlugPattern.IsMatch(normalizedFamily))
        {
            // Defence-in-depth: BinderUploadViewModel already rejects anything
            // that doesn't match this pattern, so hitting this branch means
            // the request bypassed that layer. Refuse loudly.
            throw new ArgumentException(
                "Curriculum family must be lowercase letters, digits, and hyphens (starting with a letter).",
                nameof(curriculumFamily));
        }
        if (!Enum.IsDefined(typeof(CurriculumDocumentType), documentType))
        {
            throw new ArgumentException("Unknown document type.", nameof(documentType));
        }

        var storageRoot = ResolveStorageRoot();
        var familyDir = Path.Combine(storageRoot, normalizedFamily);
        Directory.CreateDirectory(familyDir);

        // Filename: {doctype}-{yyyyMMddHHmmss}-{8-char-hex}.pdf under the
        // family folder. Timestamp is human-readable for ops; the random
        // suffix guarantees a fresh file even when two uploads for the
        // same (family, doctype) begin in the same second — otherwise
        // File.Create would truncate the live PDF the DB row still points
        // at, briefly serving a partial file until commit.
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var rand = Guid.NewGuid().ToString("N")[..8];
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(ext)) ext = ".pdf";
        // Both segments are server-controlled + validated, so string join
        // is safe. Using explicit '/' rather than Path.Combine matches the
        // DB storage convention (forward slashes cross-platform).
        var fileName = $"{documentType.ToString().ToLowerInvariant()}-{stamp}-{rand}{ext}";
        var relativePath = $"{normalizedFamily}/{fileName}";
        var absolutePath = ResolveWithinRoot(relativePath);

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
            // Resolve prior path defensively — if a legacy row somehow held
            // a rooted or escaping FilePath we log-and-skip the cleanup
            // rather than following it. The new DB row will still point at
            // the freshly-written file.
            priorAbsolutePath = TryResolveWithinRoot(existing.FilePath);
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
        // it, but the current row + file are consistent. Best-effort;
        // catch only the specific filesystem exceptions that File.Delete
        // is documented to throw so unexpected exceptions still surface.
        if (priorAbsolutePath is not null && File.Exists(priorAbsolutePath) && priorAbsolutePath != absolutePath)
        {
            try { File.Delete(priorAbsolutePath); }
            catch (IOException ex) { _logger.LogWarning(ex, "Failed to delete prior binder file {Path}", priorAbsolutePath); }
            catch (UnauthorizedAccessException ex) { _logger.LogWarning(ex, "Failed to delete prior binder file {Path}", priorAbsolutePath); }
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

        // Same defensive resolve — never trust a stored FilePath enough to
        // let File.Delete follow it outside the storage root.
        var absolutePath = TryResolveWithinRoot(doc.FilePath);

        _context.CurriculumDocuments.Remove(doc);
        await _context.SaveChangesAsync(ct);

        // Delete file after row is gone so a partial failure leaves an
        // orphan file rather than a live row pointing at a missing file.
        if (absolutePath is not null && File.Exists(absolutePath))
        {
            try { File.Delete(absolutePath); }
            catch (IOException ex) { _logger.LogWarning(ex, "Failed to delete binder file {Path}", absolutePath); }
            catch (UnauthorizedAccessException ex) { _logger.LogWarning(ex, "Failed to delete binder file {Path}", absolutePath); }
        }

        _logger.LogInformation("Deleted curriculum document {Id} ({Family}/{DocType})", doc.Id, doc.CurriculumFamily, doc.DocumentType);
    }

    /// <summary>
    /// Combine a stored relative path with the storage root and refuse the
    /// result if it escapes that root. Throws if the path is invalid — used
    /// by the write / read paths where a missing file is downstream and an
    /// escaping path is a bug we want to fail on.
    /// </summary>
    private string ResolveWithinRoot(string relativePath) =>
        TryResolveWithinRoot(relativePath)
            ?? throw new InvalidOperationException(
                $"Curriculum document path '{relativePath}' escapes the configured storage root.");

    /// <summary>
    /// Combine a stored relative path with the storage root, canonicalize,
    /// and return null (rather than throw) if the result escapes the root
    /// or is otherwise malformed. Used by cleanup paths where escaping a
    /// legacy bad row should log-and-skip, not crash the operation.
    /// Case-insensitive comparison on Windows and macOS (both have
    /// case-insensitive filesystems by default); ordinal on Linux.
    /// </summary>
    private string? TryResolveWithinRoot(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        if (Path.IsPathRooted(relativePath)) return null;

        var storageRoot = Path.GetFullPath(ResolveStorageRoot());
        var rootWithSep = storageRoot.EndsWith(Path.DirectorySeparatorChar)
            ? storageRoot
            : storageRoot + Path.DirectorySeparatorChar;

        string full;
        try
        {
            full = Path.GetFullPath(Path.Combine(storageRoot, relativePath));
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }

        var cmp = (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return full.StartsWith(rootWithSep, cmp) ? full : null;
    }

    /// <summary>
    /// Resolves the on-disk root where binder PDFs live. Prefers the
    /// configured <c>Storage:BindersDir</c> (set in appsettings or overridden
    /// via env var in prod). Falls back to <c>{contentRoot}/UploadedBinders</c>
    /// for local dev only — in non-Development environments a missing setting
    /// throws so a deploy that forgot to wire the persistent volume fails
    /// loudly rather than storing binders inside the release directory (which
    /// the deploy workflow rotates on every push, silently deleting them).
    /// Always ensures the directory exists once resolved.
    /// </summary>
    private string ResolveStorageRoot()
    {
        var configured = _configuration[StorageConfigKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (!_environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Configuration key '{StorageConfigKey}' is required outside Development. " +
                    "Point it at a persistent volume (e.g. /var/lib/{app-slug}-data/binders) — " +
                    "the release directory is rotated on every deploy and will silently drop binders.");
            }
            configured = Path.Combine(_environment.ContentRootPath, "UploadedBinders");
        }

        Directory.CreateDirectory(configured);
        return configured;
    }
}
