using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Services.Curriculum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Public-facing lesson pages — what teachers reach when they follow a
/// shortlink. Anonymous for now (any browser can view); a paid-member
/// gate can be layered later by tightening the Authorize attribute.
/// </summary>
[AllowAnonymous]
public sealed class PublicCurriculumController : Controller
{
    private readonly ApplicationDbContext _context;

    public PublicCurriculumController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("curriculum/{slug}")]
    public async Task<IActionResult> View(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();

        var day = await _context.Days
            .Include(d => d.Unit)
            .Include(d => d.WisconsinStandards)
            .Include(d => d.Videos)
            .FirstOrDefaultAsync(d => d.Slug == slug && d.IsActive, ct);
        if (day is null) return NotFound();

        var vm = LessonPrintViewModelBuilder.Build(day, publicView: true);
        return View(vm);
    }
}
