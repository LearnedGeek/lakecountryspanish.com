namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// A specific enrollable offering — a session of an after-school program at a
/// particular school, a summer camp cohort, a special-topic workshop, etc.
/// Each program has its own URL (via <see cref="Slug"/>), its own pricing, its
/// own content copy, and its own set of <see cref="ProgramEnrollment"/> rows.
///
/// Named <c>EnrollmentProgram</c> rather than <c>Program</c> to avoid the
/// collision with the top-level <c>Program</c> class the .NET SDK generates
/// from <c>Program.cs</c> — every file that would want to reference this
/// entity also lives in <c>LakeCountrySpanish.Web.*</c> and would otherwise
/// have an ambiguous <c>Program</c> resolution.
///
/// Programs are unlisted by default: they aren't linked from the main site
/// navigation. Karen shares each program's <c>/join/{slug}</c> URL as a QR code
/// at booths / open houses. When she wants a program to also appear on a
/// public /programs index, she flips <see cref="IsListed"/>.
///
/// Pricing is locked after the first save — Stripe Prices are immutable, and
/// changing a program's price would either orphan the old Stripe Price or
/// mismatch what parents saw at signup. To change pricing, duplicate the
/// program instead.
/// </summary>
public class EnrollmentProgram
{
    public int Id { get; set; }

    /// <summary>
    /// URL-safe unique identifier used in the public route (<c>/join/{slug}</c>)
    /// and rendered into QR codes. Karen picks per Program (e.g. <c>bailamos</c>,
    /// <c>bailamos-stmatthews-fall-2026</c>).
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Full display name, e.g. "Dance &amp; Learn Spanish — Fall 2026 at St Matthews".
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short tagline shown under the name on the landing page, e.g.
    /// "An After-School Spanish Enrichment Program".
    /// </summary>
    public string TagLine { get; set; } = string.Empty;

    /// <summary>
    /// Long-form Markdown description rendered as the body of the landing page.
    /// Karen can include feature bullets, country callouts, testimonials, etc.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Web-relative path to the program hero image, e.g.
    /// <c>/img/programs/dance-and-learn-spanish-after-school-program.jpeg</c>.
    /// Rendered as the hero background on the landing page.
    /// </summary>
    public string? HeroImagePath { get; set; }

    // ---------------- Logistics ----------------

    /// <summary>Location name, e.g. "St Matthew's School".</summary>
    public string LocationName { get; set; } = string.Empty;

    /// <summary>Single-line street address for the location.</summary>
    public string LocationAddress { get; set; } = string.Empty;

    /// <summary>First class date (UTC).</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Last class date (UTC).</summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Optional final date parents can sign up. Nullable — when unset, the
    /// enrollment window closes at <see cref="StartDate"/> (the program starts
    /// so no more signups). When set, the public /programs card shows an
    /// urgency indicator ("Enroll by Aug 15") and the enrollment form
    /// hard-blocks after this date.
    /// </summary>
    public DateTime? EnrollmentDeadline { get; set; }

    /// <summary>
    /// Free-text schedule description shown to parents, e.g.
    /// "Tuesdays" or "Mondays &amp; Wednesdays". Written to match how Karen
    /// wants parents to read it, not a structured enum, so a multi-day program
    /// can be described naturally.
    /// </summary>
    public string MeetingDays { get; set; } = string.Empty;

    /// <summary>Class start time (local wall-clock; TimeOnly avoids TZ ambiguity).</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>Class end time.</summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>Free-text grade range shown to parents, e.g. "3-6".</summary>
    public string GradeRange { get; set; } = string.Empty;

    /// <summary>Minimum eligible age.</summary>
    public int AgeMin { get; set; }

    /// <summary>Maximum eligible age (inclusive).</summary>
    public int AgeMax { get; set; }

    // ---------------- Pricing ----------------

    /// <summary>Full one-time price for the program (USD).</summary>
    public decimal FullPrice { get; set; }

    /// <summary>Whether the parent can choose an installment plan instead of full payment.</summary>
    public bool InstallmentsEnabled { get; set; } = true;

    /// <summary>
    /// Number of installments when installment mode is chosen (default 2).
    /// The per-installment amount is derived (<see cref="InstallmentAmount"/>),
    /// not stored, so it can't drift from <see cref="FullPrice"/>.
    /// </summary>
    public int InstallmentCount { get; set; } = 2;

    /// <summary>
    /// Date the final installment must be collected by (typically the day the
    /// program starts or shortly before). Used to compute the Stripe subscription's
    /// billing interval and <c>cancel_at</c> so Stripe auto-charges and then
    /// auto-cancels — no scheduled task on our side.
    /// </summary>
    public DateTime? FinalInstallmentDueDate { get; set; }

    /// <summary>
    /// Whether to show the "I'll pay Karen at the booth" option on the enrollment
    /// form. Useful for in-person booth signup; leave off for pure-online enrollment.
    /// </summary>
    public bool CashOptionEnabled { get; set; } = true;

    /// <summary>
    /// Derived per-installment amount rounded to cents. Single source of truth
    /// is <see cref="FullPrice"/> / <see cref="InstallmentCount"/>.
    /// </summary>
    public decimal InstallmentAmount => InstallmentCount > 0
        ? Math.Round(FullPrice / InstallmentCount, 2, MidpointRounding.AwayFromZero)
        : FullPrice;

    /// <summary>
    /// Effective deadline for parents to enroll. Falls back to <see cref="StartDate"/>
    /// (day the program begins) when the admin hasn't set an explicit deadline —
    /// they can't sign up after the program starts anyway.
    /// </summary>
    public DateTime EffectiveEnrollmentDeadline => EnrollmentDeadline ?? StartDate;

    /// <summary>True once the enrollment window has closed (past deadline).</summary>
    public bool IsEnrollmentClosed => DateTime.UtcNow > EffectiveEnrollmentDeadline;

    // ---------------- Stripe wiring (auto-populated on first Save) ----------------

    /// <summary>Stripe Product id for this Program (created via SDK when Program first saved).</summary>
    public string? StripeProductId { get; set; }

    /// <summary>One-time Stripe Price id used for the FullOneTime payment path.</summary>
    public string? StripeFullPriceId { get; set; }

    /// <summary>Recurring Stripe Price id used for the TwoInstallment (subscription) path.</summary>
    public string? StripeInstallmentPriceId { get; set; }

    // ---------------- Legal / contact ----------------

    /// <summary>Markdown-formatted waiver text shown on the enrollment form.</summary>
    public string WaiverText { get; set; } = string.Empty;

    /// <summary>Short refund policy shown as microcopy near the payment options.</summary>
    public string RefundPolicyText { get; set; } = string.Empty;

    /// <summary>Contact phone shown on the landing page (defaults to Karen's).</summary>
    public string ContactPhone { get; set; } = string.Empty;

    /// <summary>Contact email shown on the landing page.</summary>
    public string ContactEmail { get; set; } = string.Empty;

    // ---------------- Status ----------------

    /// <summary>Whether new enrollments are being accepted.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this Program appears on the public /programs calendar. Default true —
    /// most programs are meant to be discoverable so past + prospective parents
    /// can find them. Karen can flip this off for the occasional private /
    /// invite-only event.
    /// </summary>
    public bool IsListed { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ---------------- Navigation ----------------

    public virtual ICollection<ProgramEnrollment> Enrollments { get; set; } = new List<ProgramEnrollment>();
}
