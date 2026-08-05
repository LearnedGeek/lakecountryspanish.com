using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services.Programs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Admin CRUD for <see cref="EnrollmentProgram"/>. Route-prefixed so the URLs
/// read as <c>/Admin/Programs/*</c> (matching the visible admin taxonomy) even
/// though the controller name is <c>AdminPrograms</c>.
/// </summary>
[Authorize(Roles = AppRoles.Admin)]
[Route("Admin/Programs")]
public class AdminProgramsController : Controller
{
    private readonly IEnrollmentProgramService _programs;
    private readonly ApplicationDbContext _context;

    public AdminProgramsController(IEnrollmentProgramService programs, ApplicationDbContext context)
    {
        _programs = programs;
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(bool includeInactive = false)
    {
        // Include inactive here so the toggle checkbox actually surfaces them.
        var programs = await _programs.ListAllAsync(includeInactive: true);
        var counts = await _context.ProgramEnrollments
            .GroupBy(e => new { e.ProgramId, e.Status })
            .Select(g => new { g.Key.ProgramId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var items = programs
            .Where(p => includeInactive || p.IsActive)
            .Select(p => new ProgramListItemViewModel
            {
                Id = p.Id,
                Slug = p.Slug,
                Name = p.Name,
                LocationName = p.LocationName,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                FullPrice = p.FullPrice,
                IsActive = p.IsActive,
                IsListed = p.IsListed,
                EnrollmentCount = counts.Where(c => c.ProgramId == p.Id).Sum(c => c.Count),
                PaidCount = counts.Where(c => c.ProgramId == p.Id &&
                    (c.Status == ProgramEnrollmentStatus.FirstPaymentComplete ||
                     c.Status == ProgramEnrollmentStatus.FullyPaid)).Sum(c => c.Count),
                CreatedAt = p.CreatedAt
            })
            .ToList();

        ViewBag.IncludeInactive = includeInactive;
        return View(items);
    }

    [HttpGet("Create")]
    public IActionResult Create() => View("Form", new ProgramFormViewModel
    {
        StartDate = DateTime.UtcNow.Date.AddDays(14),
        EndDate = DateTime.UtcNow.Date.AddDays(14 + 56),   // 8-week default
        StartTime = new TimeOnly(15, 30),
        EndTime = new TimeOnly(16, 30),
        ContactPhone = "262-490-0304",
        ContactEmail = "info@lakecountryspanish.com",
        WaiverText = DefaultWaiverText,
        RefundPolicyText = "No refunds beyond the first week of the program.",
        InstallmentCount = 2,
        AgeMin = 8,
        AgeMax = 12,
        GradeRange = "3-6"
    });

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProgramFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", model);

        try
        {
            var created = await _programs.CreateAsync(model.ToEntity(), ct);
            TempData["SuccessMessage"] = $"Created “{created.Name}”. Grab the QR code below and share the /join/{created.Slug} URL.";
            return RedirectToAction(nameof(Detail), new { id = created.Id });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(nameof(model.Slug), $"The slug “{model.Slug}” is already used by another program.");
            return View("Form", model);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", model);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var program = await _programs.GetByIdAsync(id, ct);
        if (program is null) return NotFound();

        var counts = await _context.ProgramEnrollments
            .Where(e => e.ProgramId == id)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(ProgramEnrollmentStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

        var vm = new ProgramDetailViewModel
        {
            Program = program,
            JoinUrl = BuildJoinUrl(program.Slug),
            EnrollmentCount = counts.Sum(c => c.Count),
            PaidCount = CountOf(ProgramEnrollmentStatus.FirstPaymentComplete) + CountOf(ProgramEnrollmentStatus.FullyPaid),
            PendingCount = CountOf(ProgramEnrollmentStatus.PendingPayment),
            CashPendingCount = CountOf(ProgramEnrollmentStatus.CashPending)
        };
        return View(vm);
    }

    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var program = await _programs.GetByIdAsync(id, ct);
        if (program is null) return NotFound();
        return View("Form", ProgramFormViewModel.FromEntity(program));
    }

    [HttpPost("{id:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProgramFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View("Form", model);

        try
        {
            var updated = await _programs.UpdateAsync(model.ToEntity(), ct);
            TempData["SuccessMessage"] = $"Saved changes to “{updated.Name}”.";
            return RedirectToAction(nameof(Detail), new { id = updated.Id });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(nameof(model.Slug), $"The slug “{model.Slug}” is already used by another program.");
            return View("Form", model);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.PricingLocked = true;
            return View("Form", model);
        }
    }

    /// <summary>PNG QR code encoding the public /join/{slug} URL. Renders 300px square.</summary>
    [HttpGet("{id:int}/Qr")]
    public async Task<IActionResult> Qr(int id, CancellationToken ct)
    {
        var program = await _programs.GetByIdAsync(id, ct);
        if (program is null) return NotFound();

        var joinUrl = BuildJoinUrl(program.Slug);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 10);
        return File(png, "image/png", $"lcs-{program.Slug}-qr.png");
    }

    // ---------------- helpers ----------------

    private string BuildJoinUrl(string slug)
    {
        var scheme = Request.Scheme;
        var host = Request.Host.ToString();
        return $"{scheme}://{host}/join/{slug}";
    }

    /// <summary>
    /// Default waiver text pre-populated on the New Program form. Karen can edit
    /// per program before saving; this is a reasonable starter, not attorney-reviewed.
    /// </summary>
    private const string DefaultWaiverText = @"By enrolling my child in this program with Lake Country Spanish, LLC, I confirm and agree to the following:

**Participation.** My child has my permission to attend the program on the listed dates and times.

**Physical activity.** The program includes age-appropriate movement and activities. My child is able to participate. I will share any relevant medical conditions, allergies, or physical limitations in the Medical Concerns field so the instructor can accommodate them.

**Emergency medical care.** In the event of a medical emergency during the program, I authorize the instructor to seek appropriate care for my child and to contact my listed emergency contact immediately.

**Pickup authorization.** Only the individuals I list in the Pickup Authorization field are permitted to pick up my child after class. I will notify Lake Country Spanish in advance of any change to this list.

**Refund policy.** Program tuition is refundable only through the end of the first week of class. After that, no refunds will be issued — including for missed classes, early withdrawal, or scheduling changes on my end.

**Installment payment (if selected).** If I've chosen the 2-installment plan, I authorize the second payment to be automatically charged to my payment method approximately 30 days after signup. If the second payment cannot be collected, my child's enrollment may be discontinued.

**Release of liability.** I release Lake Country Spanish, LLC and its instructors from liability for injuries or losses occurring during the program, except those resulting from gross negligence.";
}
