using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services.Curriculum;
using LakeCountrySpanish.Web.Services.Programs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Teacher-facing binder / curriculum-document surface at
/// <c>/Curriculum/Binders</c>. View + download are open to Admin +
/// Teacher; upload + delete are Admin-only (Karen + Cece own the
/// binder content). See issue #20.
/// </summary>
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Teacher}")]
[Route("Curriculum/Binders")]
public class BindersController : Controller
{
    private readonly ICurriculumDocumentService _documents;
    private readonly IEnrollmentProgramService _programs;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BindersController> _logger;

    public BindersController(
        ICurriculumDocumentService documents,
        IEnrollmentProgramService programs,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<BindersController> logger)
    {
        _documents = documents;
        _programs = programs;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var documents = await _documents.ListAllAsync(ct);

        // Count programs per family so admin can see which curriculums have
        // no binder yet AND which families have binders but no programs.
        var programFamilyCounts = await _context.Programs
            .AsNoTracking()
            .Where(p => p.CurriculumFamily != null && p.CurriculumFamily != string.Empty)
            .GroupBy(p => p.CurriculumFamily!)
            .Select(g => new { Family = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countMap = programFamilyCounts.ToDictionary(x => x.Family, x => x.Count, StringComparer.OrdinalIgnoreCase);

        var familyGroups = documents
            .GroupBy(d => d.CurriculumFamily, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BinderFamilyGroup
            {
                CurriculumFamily = g.Key,
                ProgramCount = countMap.TryGetValue(g.Key, out var c) ? c : 0,
                Documents = g.OrderBy(d => (int)d.DocumentType).ToList()
            })
            .OrderBy(g => g.CurriculumFamily)
            .ToList();

        // Surface curriculum families that have programs but zero binders —
        // Karen sees "which curriculums still need a binder uploaded."
        var familiesWithoutBinders = countMap.Keys
            .Except(documents.Select(d => d.CurriculumFamily), StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f)
            .ToList();

        var vm = new BinderListViewModel
        {
            FamilyGroups = familyGroups,
            FamiliesWithoutBinders = familiesWithoutBinders
        };
        return View(vm);
    }

    [HttpGet("{id:int}/Download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();

        var absolutePath = _documents.GetAbsolutePath(doc);
        if (!System.IO.File.Exists(absolutePath))
        {
            _logger.LogWarning("Binder download requested for missing file — id {Id}, path {Path}", id, absolutePath);
            return NotFound();
        }

        var downloadName = string.IsNullOrWhiteSpace(doc.OriginalFileName)
            ? Path.GetFileName(doc.FilePath)
            : doc.OriginalFileName;
        return PhysicalFile(absolutePath, "application/pdf", downloadName);
    }

    [HttpGet("Upload")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Upload(string? family, CurriculumDocumentType docType = CurriculumDocumentType.TeacherBinder, CancellationToken ct = default)
    {
        var vm = new BinderUploadViewModel
        {
            CurriculumFamily = family ?? string.Empty,
            DocumentType = docType,
            ExistingCurriculumFamilies = await _programs.GetDistinctCurriculumFamiliesAsync(ct)
        };

        // If we're editing an existing (family, doctype), preload it.
        if (!string.IsNullOrWhiteSpace(family))
        {
            var normalized = family.Trim().ToLowerInvariant();
            var existing = await _context.CurriculumDocuments
                .Include(d => d.GradeBands)
                .FirstOrDefaultAsync(d => d.CurriculumFamily == normalized && d.DocumentType == docType, ct);
            if (existing is not null)
            {
                vm.IsReplace = true;
                vm.Existing = existing;
                vm.Title = existing.Title;
                vm.SelectedGradeBands = existing.GradeBands.Select(g => g.GradeBand).OrderBy(b => (int)b).ToList();
            }
        }
        return View(vm);
    }

    [HttpPost("Upload")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(30 * 1024 * 1024)] // 30 MB request cap; ViewModel enforces 25 MB per file
    public async Task<IActionResult> Upload(BinderUploadViewModel model, CancellationToken ct)
    {
        model.ExistingCurriculumFamilies = await _programs.GetDistinctCurriculumFamiliesAsync(ct);

        if (!ModelState.IsValid) return View(model);

        // Replace-in-place with no file? Just update title + bands on existing.
        var normalizedFamily = model.CurriculumFamily.Trim().ToLowerInvariant();
        if (model.IsReplace && model.Upload is null)
        {
            var existing = await _context.CurriculumDocuments
                .Include(d => d.GradeBands)
                .FirstOrDefaultAsync(d => d.CurriculumFamily == normalizedFamily && d.DocumentType == model.DocumentType, ct);
            if (existing is null) return NotFound();

            existing.Title = string.IsNullOrWhiteSpace(model.Title) ? existing.Title : model.Title!.Trim();
            existing.GradeBands.Clear();
            foreach (var band in model.SelectedGradeBands.Distinct())
            {
                existing.GradeBands.Add(new CurriculumDocumentGradeBand { GradeBand = band });
            }
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = $"Updated “{existing.Title}”.";
            return RedirectToAction(nameof(Index));
        }

        // Fresh upload OR replace with a new file.
        var uploaderId = _userManager.GetUserId(User) ?? string.Empty;
        await using var stream = model.Upload!.OpenReadStream();
        var doc = await _documents.UploadAsync(
            normalizedFamily,
            model.DocumentType,
            model.SelectedGradeBands,
            string.IsNullOrWhiteSpace(model.Title) ? Path.GetFileNameWithoutExtension(model.Upload.FileName) : model.Title!,
            stream,
            model.Upload.FileName,
            model.Upload.Length,
            uploaderId,
            ct);

        TempData["SuccessMessage"] = model.IsReplace
            ? $"Replaced binder for “{doc.CurriculumFamily}”."
            : $"Uploaded binder for “{doc.CurriculumFamily}”.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/Delete")]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        var label = string.IsNullOrWhiteSpace(doc.Title) ? doc.CurriculumFamily : doc.Title;
        await _documents.DeleteAsync(id, ct);
        TempData["SuccessMessage"] = $"Deleted binder “{label}”.";
        return RedirectToAction(nameof(Index));
    }
}
