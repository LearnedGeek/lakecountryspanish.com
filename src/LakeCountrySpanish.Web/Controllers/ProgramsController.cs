using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services.Programs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Public program calendar. Lists all <see cref="EnrollmentProgram"/> rows
/// where <c>IsListed = true</c> and <c>IsActive = true</c>, grouped by
/// "enrollment open now" vs "coming soon" vs "past". Cards link into the
/// existing <c>/join/{slug}</c> enrollment flow. Distinct from the admin
/// route at <c>/Admin/Programs</c> — this one is anonymous.
/// </summary>
[AllowAnonymous]
[Route("programs")]
public class ProgramsController : Controller
{
    private readonly IEnrollmentProgramService _programs;

    public ProgramsController(IEnrollmentProgramService programs)
    {
        _programs = programs;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var all = await _programs.ListAllAsync(includeInactive: false, ct);

        // Public view: only programs the admin flagged as listed. IsActive
        // already filters out closed enrollments. Sort by StartDate ascending
        // so the soonest program appears first.
        var listed = all.Where(p => p.IsListed).OrderBy(p => p.StartDate).ToList();

        var now = DateTime.UtcNow;
        var enrollmentOpen = listed.Where(p => p.IsEnrollmentOpen).ToList();
        var comingSoon = listed.Where(p => p.IsEnrollmentNotYetOpen).ToList();
        var closedButUpcoming = listed.Where(p => p.IsEnrollmentClosed && p.EndDate >= now).ToList();
        var past = listed.Where(p => p.EndDate < now).OrderByDescending(p => p.EndDate).ToList();

        // Group by program name (case-insensitive) so Karen's multiple-location
        // runs of the same program render as one card with a location roster.
        // Within each group, sessions ordered by soonest StartDate.
        var openGroups = enrollmentOpen
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                // Sort sessions by location first, then start time — parents
                // pick the school their kid attends first, then filter by
                // time-of-day. Representative is still the soonest-starting
                // session (drives shared header defaults) regardless of
                // display sort order.
                var displaySessions = g
                    .OrderBy(p => p.LocationName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.StartTime)
                    .ThenBy(p => p.StartDate)
                    .ToList();
                var soonest = g.OrderBy(p => p.StartDate).First();
                return new EnrollmentOpenGroup
                {
                    Representative = soonest,
                    Sessions = displaySessions,
                    MinPrice = displaySessions.Min(s => s.FullPrice),
                    MaxPrice = displaySessions.Max(s => s.FullPrice)
                };
            })
            .OrderBy(g => g.Representative.StartDate)
            .ToList();

        // Aggregate marketing stats for the banner above the grouped cards.
        var openStats = new EnrollmentOpenStats
        {
            SessionCount = enrollmentOpen.Count,
            DistinctProgramCount = openGroups.Count,
            DistinctLocationCount = enrollmentOpen
                .Select(p => p.LocationName.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            SoonestStart = enrollmentOpen.Count > 0 ? enrollmentOpen.Min(p => p.StartDate) : null,
            LatestEnd = enrollmentOpen.Count > 0 ? enrollmentOpen.Max(p => p.EndDate) : null
        };

        var vm = new ProgramsCalendarViewModel
        {
            OpenStats = openStats,
            EnrollmentOpenGroups = openGroups,
            ComingSoon = comingSoon,
            ClosedButUpcoming = closedButUpcoming,
            Past = past
        };
        return View(vm);
    }

    /// <summary>
    /// Public read-only detail page for one program. Renders the full Markdown
    /// description + all logistics so parents can browse-and-decide before
    /// hitting the enrollment form at <c>/join/{slug}</c>. Separate from
    /// <c>/join/{slug}</c> so the browse experience isn't mixed with a wall
    /// of form fields.
    /// </summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var program = await _programs.GetBySlugAsync(slug, ct);
        if (program is null || !program.IsListed) return NotFound();
        return View(program);
    }
}
