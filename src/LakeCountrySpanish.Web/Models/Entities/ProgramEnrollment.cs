namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// One family's signup for a <see cref="Program"/>. Captures parent contact
/// info, one child's details, emergency contact, pickup authorization, waiver
/// acceptance, and the Stripe / cash payment path chosen.
///
/// Anonymous — no <c>ApplicationUser</c> is required (parents don't need an
/// LCS account to enroll a child in an open-house Program). If Karen wants
/// per-parent history later, we can link enrollments to accounts retroactively
/// by email match.
///
/// One enrollment == one child. A parent with two kids submits the form twice.
/// This is intentional for v1: keeps the form short and matches the
/// booth-signup flow where a parent enrolls one kid at a time.
/// </summary>
public class ProgramEnrollment
{
    public int Id { get; set; }

    public int ProgramId { get; set; }
    public virtual EnrollmentProgram Program { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ---------------- Parent ----------------

    public string ParentFirstName { get; set; } = string.Empty;
    public string ParentLastName { get; set; } = string.Empty;
    public string ParentEmail { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;

    public string ParentAddressLine1 { get; set; } = string.Empty;
    public string ParentCity { get; set; } = string.Empty;
    public string ParentState { get; set; } = string.Empty;
    public string ParentZip { get; set; } = string.Empty;

    // ---------------- Student ----------------

    public string StudentFirstName { get; set; } = string.Empty;
    public string StudentLastName { get; set; } = string.Empty;

    /// <summary>
    /// School-placement grade the parent enters (e.g. "3", "4", "5", "6", "Other").
    /// Kept alongside <see cref="StudentBirthDate"/> because school grade can differ
    /// from chronological age (retained / accelerated) and Karen may want both.
    /// </summary>
    public string StudentGrade { get; set; } = string.Empty;

    public DateOnly StudentBirthDate { get; set; }

    /// <summary>
    /// Free-form medical concerns the instructor should know about: allergies,
    /// current medications, chronic conditions, sensory considerations, etc.
    /// Broader than "allergies" to encourage disclosure.
    /// </summary>
    public string? MedicalConcerns { get; set; }

    /// <summary>Anything else the parent wants Karen to know.</summary>
    public string? StudentNotes { get; set; }

    // ---------------- Emergency contact ----------------

    public string EmergencyName { get; set; } = string.Empty;
    public string EmergencyPhone { get; set; } = string.Empty;
    public string EmergencyRelationship { get; set; } = string.Empty;

    // ---------------- Pickup authorization ----------------

    /// <summary>
    /// Free-form list of people authorized to pick up the child. Parent writes
    /// names / relationships (and phone if desired) as one-per-line or comma-separated.
    /// </summary>
    public string PickupAuthorization { get; set; } = string.Empty;

    // ---------------- Consent ----------------

    /// <summary>Timestamp when the parent checked the waiver-acceptance box. Required for enrollment.</summary>
    public DateTime WaiverAcceptedAt { get; set; }

    /// <summary>
    /// Timestamp when the parent granted the photo/video release. Null when
    /// the parent left the release box unchecked. Consistent with
    /// <see cref="WaiverAcceptedAt"/> so the audit trail is timestamped either way.
    /// </summary>
    public DateTime? PhotoReleaseGrantedAt { get; set; }

    // ---------------- Payment ----------------

    public ProgramPaymentType PaymentType { get; set; }
    public ProgramEnrollmentStatus Status { get; set; } = ProgramEnrollmentStatus.PendingPayment;

    public string? StripeCheckoutSessionId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }

    public DateTime? FirstPaymentAt { get; set; }
    public DateTime? SecondPaymentAt { get; set; }

    /// <summary>Rolling sum of amounts received (across installments). Useful for admin display.</summary>
    public decimal TotalAmountPaid { get; set; }

    // ---------------- Admin ----------------

    /// <summary>Karen's private notes on this enrollment (not shown to the parent).</summary>
    public string? AdminNotes { get; set; }
}
