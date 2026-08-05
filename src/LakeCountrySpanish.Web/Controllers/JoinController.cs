using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services.Programs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Public parent-facing enrollment landing pages at <c>/join/{slug}</c>. Fully
/// anonymous — no LCS account required. Each Program's <c>Slug</c> is what
/// Karen encodes into the QR code she prints for the booth.
/// </summary>
[AllowAnonymous]
[Route("join")]
public class JoinController : Controller
{
    private readonly IEnrollmentProgramService _programs;
    private readonly IProgramEnrollmentService _enrollments;
    private readonly ILogger<JoinController> _logger;

    public JoinController(
        IEnrollmentProgramService programs,
        IProgramEnrollmentService enrollments,
        ILogger<JoinController> logger)
    {
        _programs = programs;
        _enrollments = enrollments;
        _logger = logger;
    }

    /// <summary>Landing page: hero, program summary, enrollment form.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Landing(string slug, bool cancelled = false, CancellationToken ct = default)
    {
        var program = await _programs.GetBySlugAsync(slug, ct);
        if (program is null || !program.IsActive) return NotFound();

        if (cancelled)
        {
            TempData["InfoMessage"] = "Payment was cancelled — your enrollment wasn't saved. Try again below.";
        }

        var vm = new ProgramEnrollmentFormViewModel
        {
            ProgramId = program.Id,
            ProgramSlug = program.Slug,
            Program = program,
            ParentState = "WI",
            PaymentType = program.InstallmentsEnabled
                ? ProgramPaymentType.FullOneTime  // default to full even when installments offered
                : ProgramPaymentType.FullOneTime
        };
        return View(vm);
    }

    /// <summary>Form submission: save enrollment + kick off Stripe (or short-circuit for cash).</summary>
    [HttpPost("{slug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string slug, ProgramEnrollmentFormViewModel model, CancellationToken ct)
    {
        var program = await _programs.GetBySlugAsync(slug, ct);
        if (program is null || !program.IsActive) return NotFound();

        // Repopulate the Program-linked fields so the form can re-render on validation failure.
        model.ProgramId = program.Id;
        model.ProgramSlug = program.Slug;
        model.Program = program;

        if (!ModelState.IsValid) return View("Landing", model);

        var submission = new EnrollmentSubmission
        {
            ProgramId = program.Id,
            PaymentType = model.PaymentType,
            ParentFirstName = model.ParentFirstName,
            ParentLastName = model.ParentLastName,
            ParentEmail = model.ParentEmail,
            ParentPhone = model.ParentPhone,
            ParentAddressLine1 = model.ParentAddressLine1,
            ParentCity = model.ParentCity,
            ParentState = model.ParentState,
            ParentZip = model.ParentZip,
            StudentFirstName = model.StudentFirstName,
            StudentLastName = model.StudentLastName,
            StudentGrade = model.StudentGrade,
            StudentBirthDate = model.StudentBirthDate,
            MedicalConcerns = model.MedicalConcerns,
            StudentNotes = model.StudentNotes,
            EmergencyName = model.EmergencyName,
            EmergencyPhone = model.EmergencyPhone,
            EmergencyRelationship = model.EmergencyRelationship,
            PickupAuthorization = model.PickupAuthorization,
            WaiverAccepted = model.WaiverAccepted,
            PhotoReleaseGranted = model.PhotoReleaseGranted,
            BaseUrl = $"{Request.Scheme}://{Request.Host}"
        };

        var result = await _enrollments.SubmitAsync(submission, ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Something went wrong. Please try again.");
            return View("Landing", model);
        }

        if (result.IsCashInHand)
        {
            return RedirectToAction(nameof(ThankYou), new { slug, id = result.EnrollmentId });
        }

        // Stripe path — redirect to the hosted Checkout URL.
        return Redirect(result.StripeCheckoutUrl!);
    }

    /// <summary>
    /// Thank-you landing after a successful Stripe checkout or cash-in-hand
    /// submission. Two lookup paths: Stripe session id (from success_url) or
    /// enrollment id (from cash path). Whichever is present wins.
    /// </summary>
    [HttpGet("{slug}/thank-you")]
    public async Task<IActionResult> ThankYou(string slug, string? session = null, int? id = null, CancellationToken ct = default)
    {
        var program = await _programs.GetBySlugAsync(slug, ct);
        if (program is null) return NotFound();

        ProgramEnrollment? enrollment = null;
        if (!string.IsNullOrEmpty(session))
        {
            enrollment = await _enrollments.GetByCheckoutSessionAsync(session, ct);
        }
        else if (id.HasValue)
        {
            enrollment = await _enrollments.GetAsync(id.Value, ct);
        }

        if (enrollment is null || enrollment.ProgramId != program.Id)
        {
            _logger.LogWarning("Thank-you page hit for slug {Slug} but no matching enrollment (session={Session}, id={Id})", slug, session, id);
            return NotFound();
        }

        return View(new ProgramEnrollmentThankYouViewModel
        {
            Program = program,
            Enrollment = enrollment
        });
    }
}
