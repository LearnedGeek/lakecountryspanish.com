using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Admin surface for the vocab catalog that drives generated worksheets.
/// First action is "generate missing images for a theme" — the rest of the
/// surface (CRUD on terms, browse images per term, regenerate one) ships in
/// later passes.
/// </summary>
[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/Vocab")]
public class VocabAdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IVocabImageGenerationService _generator;
    private readonly IAiImageService _ai;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<VocabAdminController> _logger;

    public VocabAdminController(
        ApplicationDbContext context,
        IVocabImageGenerationService generator,
        IAiImageService ai,
        UserManager<ApplicationUser> userManager,
        ILogger<VocabAdminController> logger)
    {
        _context = context;
        _generator = generator;
        _ai = ai;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Per-theme summary: total terms, how many already have images.
        var byTheme = await _context.VocabTerms
            .GroupBy(v => v.Theme)
            .Select(g => new VocabThemeSummary
            {
                Theme = g.Key,
                TotalTerms = g.Count(),
                WithImages = g.Count(v => v.MediaAssetId != null)
            })
            .OrderBy(s => s.Theme)
            .ToListAsync();

        var vm = new VocabAdminIndexViewModel
        {
            AiProviderName = _ai.ProviderName,
            AiModelId = _ai.ModelId,
            AiAvailable = _ai.IsAvailable,
            Themes = byTheme
        };

        return View(vm);
    }

    [HttpPost("GenerateImages/{theme}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateImages(string theme, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User) ?? string.Empty;

        try
        {
            var summary = await _generator.GenerateMissingForThemeAsync(theme, userId, ct);
            TempData["GenerationSummary"] = System.Text.Json.JsonSerializer.Serialize(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image generation failed for theme {Theme}", theme);
            TempData["Error"] = $"Image generation failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}

public sealed class VocabAdminIndexViewModel
{
    public string AiProviderName { get; set; } = string.Empty;
    public string AiModelId { get; set; } = string.Empty;
    public bool AiAvailable { get; set; }
    public List<VocabThemeSummary> Themes { get; set; } = new();
}

public sealed class VocabThemeSummary
{
    public string Theme { get; set; } = string.Empty;
    public int TotalTerms { get; set; }
    public int WithImages { get; set; }
    public int Missing => TotalTerms - WithImages;
}
