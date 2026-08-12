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

        var vm = new ProgramsCalendarViewModel
        {
            EnrollmentOpen = enrollmentOpen,
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
