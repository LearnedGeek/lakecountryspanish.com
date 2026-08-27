using System.Security.Claims;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services.Programs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using SkiaSharp;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Admin CRUD for <see cref="EnrollmentProgram"/>. Route-prefixed so the URLs
/// read as <c>/Admin/Programs/*</c> (matching the visible admin taxonomy) even
/// though the controller name is <c>AdminPrograms</c>.
///
/// Authorized for Admin OR Teacher — program creation is the co-founders'
/// operational domain (Karen + Cece own their sessions), and requiring Mark
/// to be a bottleneck for every new program doesn't scale. Other /Admin/*
/// controllers (system config, students, dashboards) stay Admin-only. Same
/// dual-role pattern <see cref="CurriculumController"/> uses.
/// </summary>
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Teacher}")]
[Route("Admin/Programs")]
public class AdminProgramsController : Controller
{
    private readonly IEnrollmentProgramService _programs;
    private readonly IProgramEnrollmentService _enrollments;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxHeroImageBytes = 5 * 1024 * 1024; // 5 MB

    public AdminProgramsController(
        IEnrollmentProgramService programs,
        IProgramEnrollmentService enrollments,
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _programs = programs;
        _enrollments = enrollments;
        _context = context;
        _environment = environment;
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
        if (!AnyMeetingDayPicked(model))
        {
            ModelState.AddModelError(nameof(model.MeetingDaySun), "Please pick at least one meeting day.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        if (!TryHandleHeroImageUpload(model, out var uploadError))
        {
            ModelState.AddModelError(nameof(model.HeroImageUpload), uploadError!);
            return View("Form", model);
        }

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
        if (!AnyMeetingDayPicked(model))
        {
            ModelState.AddModelError(nameof(model.MeetingDaySun), "Please pick at least one meeting day.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        if (!TryHandleHeroImageUpload(model, out var uploadError))
        {
            ModelState.AddModelError(nameof(model.HeroImageUpload), uploadError!);
            return View("Form", model);
        }

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

    /// <summary>
    /// Hard-delete a program. Service enforces the "no delete once someone
    /// enrolled" rule; if that trips we surface the friendly error back to
    /// Detail so Karen sees the archive-instead suggestion.
    /// </summary>
    [HttpPost("{id:int}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var program = await _programs.GetByIdAsync(id, ct);
        if (program is null) return NotFound();

        var name = program.Name;
        try
        {
            await _programs.DeleteAsync(id, ct);
            TempData["SuccessMessage"] = $"Deleted \"{name}\".";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id });
        }
    }

    /// <summary>
    /// PNG QR code encoding the public /join/{slug} URL, with an identifying label
    /// composited below (program name + location + short URL). The label baked into
    /// the image itself matters when Karen prints a stack of QRs and hands them off
    /// to a print shop or for flyer copying — otherwise she can't tell which is which.
    /// </summary>
    [HttpGet("{id:int}/Qr")]
    public async Task<IActionResult> Qr(int id, CancellationToken ct)
    {
        var program = await _programs.GetByIdAsync(id, ct);
        if (program is null) return NotFound();

        var joinUrl = BuildJoinUrl(program.Slug);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var qrPng = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 12);

        var labeledPng = ComposeLabeledQr(qrPng, program, joinUrl);
        return File(labeledPng, "image/png", $"lcs-{program.Slug}-qr.png");
    }

    /// <summary>
    /// Composites the raw QR PNG onto a taller white canvas with program name +
    /// location + date range + URL rendered below it in SkiaSharp. The URL is
    /// intentionally kept in the label because it's the only quick way to
    /// distinguish a stg vs prod QR when Karen is holding a printed stack.
    /// Falls back to the original unlabeled PNG if no usable system font is
    /// available (the QR still scans — parents just don't see the identifying text).
    /// </summary>
    private static byte[] ComposeLabeledQr(byte[] qrPng, EnrollmentProgram program, string joinUrl)
    {
        using var qrBitmap = SKBitmap.Decode(qrPng);
        if (qrBitmap is null) return qrPng;

        var titleTypeface = ResolveTypeface(SKFontStyle.Bold);
        var bodyTypeface = ResolveTypeface(SKFontStyle.Normal);

        if (titleTypeface is null || bodyTypeface is null)
        {
            // No usable font — return the raw QR so at least scanning still works.
            return qrPng;
        }

        using (titleTypeface)
        using (bodyTypeface)
        using (var titleFont = new SKFont(titleTypeface, 26f))
        using (var subtitleFont = new SKFont(bodyTypeface, 16f))
        using (var dateFont = new SKFont(bodyTypeface, 15f))
        using (var urlFont = new SKFont(bodyTypeface, 13f))
        {
            const float sidePadding = 16f;
            const float topPadding = 16f;
            const float gapAboveText = 20f;
            const float lineGap = 6f;

            var title = program.Name;
            var subtitle = string.IsNullOrEmpty(program.LocationName) ? program.MeetingDays : program.LocationName;
            var dateLine = FormatDateRange(program.StartDate, program.EndDate);
            var urlLine = joinUrl.Replace("https://", "").Replace("http://", "");

            var canvasWidth = qrBitmap.Width + (int)(sidePadding * 2);
            var textBlockHeight = titleFont.Size + lineGap + subtitleFont.Size + lineGap + dateFont.Size + lineGap + urlFont.Size;
            var canvasHeight = (int)(topPadding + qrBitmap.Height + gapAboveText + textBlockHeight + topPadding);

            var info = new SKImageInfo(canvasWidth, canvasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            canvas.Clear(SKColors.White);

            // Draw the QR centered horizontally at the top.
            canvas.DrawBitmap(qrBitmap, sidePadding, topPadding);

            using var titlePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var subtitlePaint = new SKPaint { Color = new SKColor(0x4b, 0x55, 0x63), IsAntialias = true };
            using var datePaint = new SKPaint { Color = new SKColor(0x4b, 0x55, 0x63), IsAntialias = true };
            using var urlPaint = new SKPaint { Color = new SKColor(0x6b, 0x72, 0x80), IsAntialias = true };

            var centerX = canvasWidth / 2f;
            var textY = topPadding + qrBitmap.Height + gapAboveText + titleFont.Size;

            DrawCenteredText(canvas, title, titleFont, titlePaint, centerX, textY, canvasWidth - sidePadding * 2);
            textY += lineGap + subtitleFont.Size;
            DrawCenteredText(canvas, subtitle, subtitleFont, subtitlePaint, centerX, textY, canvasWidth - sidePadding * 2);
            textY += lineGap + dateFont.Size;
            DrawCenteredText(canvas, dateLine, dateFont, datePaint, centerX, textY, canvasWidth - sidePadding * 2);
            textY += lineGap + urlFont.Size;
            DrawCenteredText(canvas, urlLine, urlFont, urlPaint, centerX, textY, canvasWidth - sidePadding * 2);

            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
    }

    /// <summary>
    /// Human-friendly date range for the QR label. Collapses when start and end
    /// share a year ("Aug 19 – Oct 14, 2026") and further when they share a month
    /// ("Aug 5 – 12, 2026"). Renders a single date if start == end.
    /// </summary>
    private static string FormatDateRange(DateTime start, DateTime end)
    {
        if (start.Date == end.Date) return start.ToString("MMM d, yyyy");
        if (start.Year == end.Year)
        {
            if (start.Month == end.Month) return $"{start:MMM d} – {end:d}, {end:yyyy}";
            return $"{start:MMM d} – {end:MMM d, yyyy}";
        }
        return $"{start:MMM d, yyyy} – {end:MMM d, yyyy}";
    }

    /// <summary>
    /// Finds a workable typeface by trying common cross-platform families in order,
    /// then falling back to the platform default. Returns null only if the system
    /// has no fonts at all — extremely unusual, but the caller degrades gracefully.
    /// </summary>
    private static SKTypeface? ResolveTypeface(SKFontStyle style)
    {
        // "DejaVu Sans" — bundled with Ubuntu / Debian by default (fonts-dejavu-core)
        // "Arial" — bundled with Windows
        // "Helvetica" — bundled with macOS
        // Default — whatever SkiaSharp picks from the platform font manager
        foreach (var family in new[] { "DejaVu Sans", "Liberation Sans", "Arial", "Helvetica" })
        {
            var tf = SKFontManager.Default.MatchFamily(family, style);
            if (tf is not null) return tf;
        }
        return SKFontManager.Default.MatchFamily(null, style)
            ?? SKTypeface.Default;
    }

    /// <summary>Draws text centered on <paramref name="centerX"/>, truncated with an ellipsis if it exceeds <paramref name="maxWidth"/>.</summary>
    private static void DrawCenteredText(SKCanvas canvas, string text, SKFont font, SKPaint paint, float centerX, float y, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return;

        var display = text;
        var measured = font.MeasureText(display);
        if (measured > maxWidth)
        {
            // Trim character-by-character until it fits, then append an ellipsis.
            while (display.Length > 1 && font.MeasureText(display + "…") > maxWidth)
            {
                display = display[..^1];
            }
            display += "…";
        }

        var textWidth = font.MeasureText(display);
        var x = centerX - textWidth / 2f;
        canvas.DrawText(display, x, y, SKTextAlign.Left, font, paint);
    }

    /// <summary>
    /// Printable QR card — the QR code plus a human-readable label (program name,
    /// location, dates, meeting days, join URL) so Karen can print a stack of
    /// cards for different booths and still tell them apart. Rendered as HTML
    /// with a print stylesheet so Karen picks paper size / orientation from the
    /// browser's Print dialog. No new server-side image compositing needed.
    /// </summary>
    [HttpGet("{id:int}/PrintCard")]
    public async Task<IActionResult> PrintCard(int id, CancellationToken ct)
    {
        var program = await _programs.GetByIdAsync(id, ct);
        if (program is null) return NotFound();

        var vm = new ProgramPrintCardViewModel
        {
            Program = program,
            JoinUrl = BuildJoinUrl(program.Slug),
            QrImageUrl = Url.Action(nameof(Qr), new { id = program.Id })!
        };
        return View(vm);
    }

    /// <summary>Full roster of enrollments for a program — admin-only view.</summary>
    [HttpGet("{id:int}/Enrollments")]
    public async Task<IActionResult> Enrollments(int id, CancellationToken ct)
    {
        var program = await _programs.GetByIdAsync(id, ct);
        if (program is null) return NotFound();

        var enrollments = await _context.ProgramEnrollments
            .Where(e => e.ProgramId == id)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        // Bulk-load every audit event for this program's enrollments in one round
        // trip, then bucket by enrollment id — avoids an N+1 query as the roster grows.
        var enrollmentIds = enrollments.Select(e => e.Id).ToList();
        var auditEvents = await _context.ProgramEnrollmentAuditEvents
            .Where(a => enrollmentIds.Contains(a.EnrollmentId))
            .OrderBy(a => a.OccurredAt)
            .ToListAsync(ct);

        var auditByEnrollment = auditEvents
            .GroupBy(a => a.EnrollmentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProgramEnrollmentAuditEvent>)g.ToList());

        var vm = new ProgramEnrollmentsRosterViewModel
        {
            Program = program,
            Enrollments = enrollments,
            AuditEventsByEnrollmentId = auditByEnrollment
        };
        return View(vm);
    }

    /// <summary>CSV export of the enrollment roster — includes contact + emergency + medical fields for printing a paper roster.</summary>
    [HttpGet("{id:int}/Enrollments.csv")]
    public async Task<IActionResult> EnrollmentsCsv(int id, CancellationToken ct)
    {
        var program = await _programs.GetByIdAsync(id, ct);
        if (program is null) return NotFound();

        var enrollments = await _context.ProgramEnrollments
            .Where(e => e.ProgramId == id)
            .OrderBy(e => e.StudentLastName)
            .ThenBy(e => e.StudentFirstName)
            .ToListAsync(ct);

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Enrolled,Student,Grade,Birthdate,Parent,Email,Phone,Address,City,State,Zip,Emergency Name,Emergency Phone,Emergency Relationship,Pickup Authorization,Medical Concerns,Notes,Payment Type,Status,Amount Paid,Waiver Accepted,Photo Release");

        foreach (var e in enrollments)
        {
            csv.Append(CsvField(e.CreatedAt.ToString("yyyy-MM-dd HH:mm"))).Append(',');
            csv.Append(CsvField($"{e.StudentFirstName} {e.StudentLastName}")).Append(',');
            csv.Append(CsvField(e.StudentGrade)).Append(',');
            csv.Append(CsvField(e.StudentBirthDate.ToString("yyyy-MM-dd"))).Append(',');
            csv.Append(CsvField($"{e.ParentFirstName} {e.ParentLastName}")).Append(',');
            csv.Append(CsvField(e.ParentEmail)).Append(',');
            csv.Append(CsvField(e.ParentPhone)).Append(',');
            csv.Append(CsvField(e.ParentAddressLine1)).Append(',');
            csv.Append(CsvField(e.ParentCity)).Append(',');
            csv.Append(CsvField(e.ParentState)).Append(',');
            csv.Append(CsvField(e.ParentZip)).Append(',');
            csv.Append(CsvField(e.EmergencyName)).Append(',');
            csv.Append(CsvField(e.EmergencyPhone)).Append(',');
            csv.Append(CsvField(e.EmergencyRelationship)).Append(',');
            csv.Append(CsvField(e.PickupAuthorization)).Append(',');
            csv.Append(CsvField(e.MedicalConcerns ?? string.Empty)).Append(',');
            csv.Append(CsvField(e.StudentNotes ?? string.Empty)).Append(',');
            csv.Append(CsvField(e.PaymentType.ToString())).Append(',');
            csv.Append(CsvField(e.Status.ToString())).Append(',');
            csv.Append(CsvField(e.TotalAmountPaid.ToString("F2"))).Append(',');
            csv.Append(CsvField(e.WaiverAcceptedAt.ToString("yyyy-MM-dd HH:mm"))).Append(',');
            csv.Append(CsvField(e.PhotoReleaseGrantedAt?.ToString("yyyy-MM-dd HH:mm") ?? "no"));
            csv.AppendLine();
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        var fileName = $"lcs-{program.Slug}-roster-{DateTime.UtcNow:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>Mark a cash-in-hand enrollment as paid once the admin has the cash.</summary>
    [HttpPost("{id:int}/Enrollments/{enrollmentId:int}/ConfirmCash")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCash(int id, int enrollmentId, CancellationToken ct)
    {
        try
        {
            var enrollment = await _enrollments.MarkCashConfirmedAsync(enrollmentId, CurrentActor(), ct);
            TempData["SuccessMessage"] = $"Marked cash received from {enrollment.ParentFirstName} {enrollment.ParentLastName} for {enrollment.StudentFirstName}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Enrollments), new { id });
    }

    /// <summary>Reverse a prior cash confirmation — writes a countervailing audit event so the trail shows both actions.</summary>
    [HttpPost("{id:int}/Enrollments/{enrollmentId:int}/UndoCashConfirmation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UndoCashConfirmation(int id, int enrollmentId, string? reason, CancellationToken ct)
    {
        try
        {
            var enrollment = await _enrollments.UndoCashConfirmationAsync(enrollmentId, CurrentActor(), reason, ct);
            TempData["SuccessMessage"] = $"Reversed cash confirmation for {enrollment.ParentFirstName} {enrollment.ParentLastName} — enrollment is back to cash-pending.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Enrollments), new { id });
    }

    // ---------------- helpers ----------------

    /// <summary>Constructs an <see cref="AdminActor"/> from the current ClaimsPrincipal for audit-event attribution.</summary>
    private AdminActor CurrentActor() => new(
        UserId: User.FindFirstValue(ClaimTypes.NameIdentifier),
        DisplayName: User.Identity?.Name ?? "unknown");

    /// <summary>RFC 4180 CSV field escape — wraps in quotes if the field contains a comma, quote, or newline; doubles internal quotes.</summary>
    private static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needsQuoting = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuoting) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static bool AnyMeetingDayPicked(ProgramFormViewModel model) =>
        model.MeetingDaySun || model.MeetingDayMon || model.MeetingDayTue ||
        model.MeetingDayWed || model.MeetingDayThu || model.MeetingDayFri ||
        model.MeetingDaySat;

    private string BuildJoinUrl(string slug)
    {
        var scheme = Request.Scheme;
        var host = Request.Host.ToString();
        return $"{scheme}://{host}/join/{slug}";
    }

    /// <summary>
    /// Validates and persists both the hero-image and event-image uploads (either
    /// or both may be null / empty). Non-empty uploads are saved to
    /// <c>wwwroot/img/programs/{slug}[-event].{ext}</c> and the corresponding
    /// property on the view model is set to the web-relative path. Any prior
    /// file for the same slot with a different extension is cleaned up.
    /// </summary>
    private bool TryHandleHeroImageUpload(ProgramFormViewModel model, out string? error)
    {
        if (!TrySaveProgramImage(model.HeroImageUpload, model.Slug, suffix: "", out var heroPath, out error))
            return false;
        if (heroPath is not null) model.HeroImagePath = heroPath;

        if (!TrySaveProgramImage(model.EventImageUpload, model.Slug, suffix: "-event", out var eventPath, out error))
            return false;
        if (eventPath is not null) model.EventImagePath = eventPath;

        return true;
    }

    /// <summary>
    /// Shared upload plumbing: validates the file, writes it to
    /// <c>wwwroot/img/programs/{slug}{suffix}.{ext}</c>, cleans up prior
    /// extensions for the same slot, and returns the web-relative path. No-op
    /// (returns true, savedPath=null) when no file was selected — the caller
    /// preserves whatever path was already on the model.
    /// </summary>
    private bool TrySaveProgramImage(IFormFile? upload, string slug, string suffix, out string? savedPath, out string? error)
    {
        savedPath = null;
        error = null;

        if (upload is null || upload.Length == 0) return true;

        if (upload.Length > MaxHeroImageBytes)
        {
            error = $"Image is too large ({upload.Length / (1024 * 1024)} MB). Max 5 MB.";
            return false;
        }

        var ext = Path.GetExtension(upload.FileName);
        if (!AllowedImageExtensions.Contains(ext))
        {
            error = "Image must be a .jpg, .jpeg, .png, or .webp file.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            error = "Please fill in the URL slug before uploading an image — the filename is derived from it.";
            return false;
        }

        // Normalize .jpeg → .jpg so /img/programs/foo.jpg is the canonical stored path.
        var normalizedExt = ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ext.ToLowerInvariant();
        var fileStem = $"{slug}{suffix}";
        var relativePath = $"/img/programs/{fileStem}{normalizedExt}";
        var absoluteDir = Path.Combine(_environment.WebRootPath, "img", "programs");
        var absolutePath = Path.Combine(absoluteDir, $"{fileStem}{normalizedExt}");

        Directory.CreateDirectory(absoluteDir);

        // Remove any prior file for the same slot with a different extension so
        // an orphan doesn't shadow the current image.
        foreach (var otherExt in AllowedImageExtensions)
        {
            var normalized = otherExt.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : otherExt.ToLowerInvariant();
            if (normalized == normalizedExt) continue;
            var prior = Path.Combine(absoluteDir, $"{fileStem}{normalized}");
            if (System.IO.File.Exists(prior))
            {
                try { System.IO.File.Delete(prior); } catch { /* best-effort cleanup */ }
            }
        }

        using (var stream = System.IO.File.Create(absolutePath))
        {
            upload.CopyTo(stream);
        }

        savedPath = relativePath;
        return true;
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
