using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Renders printable worksheets generated from the vocab catalog. First
/// surface is the scavenger-hunt template; bingo, matching, and flashcard
/// templates slot in here as the catalog grows.
/// </summary>
[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)]
[Route("Worksheets")]
public class WorksheetsController : Controller
{
    private readonly ApplicationDbContext _context;

    public WorksheetsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Scavenger-hunt worksheet for the given vocab theme. Pulls all terms
    /// in the theme that have a generated image and renders them in the
    /// IMG_8908 list layout (numbered list, image left, Spanish word
    /// middle, tally column on the right).
    /// </summary>
    [HttpGet("ScavengerHunt/{theme}")]
    public async Task<IActionResult> ScavengerHunt(string theme)
    {
        var terms = await _context.VocabTerms
            .Include(v => v.MediaAsset)
            .Where(v => v.Theme == theme && v.MediaAssetId != null)
            .OrderBy(v => v.Spanish)
            .ToListAsync();

        if (terms.Count == 0)
        {
            return View("ScavengerHuntEmpty", theme);
        }

        var vm = new ScavengerHuntViewModel
        {
            Theme = theme,
            Title = "¡Busca y marca!",
            Subtitle = "Scavenger Hunt",
            Items = terms.Select(t => new ScavengerHuntItem
            {
                Article = t.Article,
                Spanish = t.Spanish,
                English = t.English,
                ImagePath = t.MediaAsset!.FilePath
            }).ToList()
        };

        return View(vm);
    }
}

public sealed class ScavengerHuntViewModel
{
    public string Theme { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public List<ScavengerHuntItem> Items { get; set; } = new();
}

public sealed class ScavengerHuntItem
{
    public string Article { get; set; } = string.Empty;
    public string Spanish { get; set; } = string.Empty;
    public string English { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}
