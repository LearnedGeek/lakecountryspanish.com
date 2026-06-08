using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Resolves <c>lcs/{code}</c> shortlinks. Anonymous so teachers can type the
/// code from any browser; the destination page enforces its own auth.
/// </summary>
[AllowAnonymous]
public sealed class ShortlinkController : Controller
{
    private readonly ApplicationDbContext _context;

    public ShortlinkController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /lcs/{code} → 302 to the resolved destination.
    /// Bumps ClickCount + LastClickedAt as a side effect.
    /// </summary>
    [HttpGet("lcs/{code}")]
    public async Task<IActionResult> Resolve(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return NotFound();

        // Match case-insensitively so "lcs/ezz" and "lcs/EZZ" both resolve;
        // codes are stored uppercase.
        var normalized = code.Trim().ToUpperInvariant();
        var link = await _context.Shortlinks.FirstOrDefaultAsync(s => s.Code == normalized, ct);
        if (link is null) return NotFound();

        link.ClickCount += 1;
        link.LastClickedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return link.DestinationType switch
        {
            ShortlinkDestination.Lesson => await ResolveLessonAsync(link.DestinationId, ct),
            ShortlinkDestination.Video  => await ResolveVideoAsync(link.DestinationId, ct),
            _                           => NotFound()
        };
    }

    private async Task<IActionResult> ResolveLessonAsync(int dayId, CancellationToken ct)
    {
        var slug = await _context.Days
            .Where(d => d.Id == dayId)
            .Select(d => d.Slug)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();
        return Redirect($"/curriculum/{slug}");
    }

    private async Task<IActionResult> ResolveVideoAsync(int videoId, CancellationToken ct)
    {
        var video = await _context.LessonVideos
            .Where(v => v.Id == videoId)
            .Select(v => new { v.Url })
            .FirstOrDefaultAsync(ct);
        if (video is null || string.IsNullOrWhiteSpace(video.Url)) return NotFound();
        return Redirect(video.Url);
    }
}
