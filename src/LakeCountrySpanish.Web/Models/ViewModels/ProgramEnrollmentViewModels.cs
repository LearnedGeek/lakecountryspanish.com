using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// The parent-facing enrollment form model. Bound from the POST /join/{slug}
/// submission; validation attributes enforce required fields.
/// </summary>
public sealed class ProgramEnrollmentFormViewModel
{
    // Populated by the controller from the URL slug — parent doesn't fill it.
    public int ProgramId { get; set; }
    public string ProgramSlug { get; set; } = string.Empty;
    public EnrollmentProgram Program { get; set; } = null!;

    // ---------------- Parent ----------------

    [Required, StringLength(80)]
    [Display(Name = "Parent first name")]
    public string ParentFirstName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    [Display(Name = "Parent last name")]
    public string ParentLastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    [Display(Name = "Email")]
    public string ParentEmail { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "Phone")]
    public string ParentPhone { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Street address")]
    public string ParentAddressLine1 { get; set; } = string.Empty;

    [Required, StringLength(80)]
    [Display(Name = "City")]
    public string ParentCity { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "State")]
    public string ParentState { get; set; } = "WI";

    [Required, StringLength(20)]
    [Display(Name = "ZIP")]
    public string ParentZip { get; set; } = string.Empty;

    // ---------------- Student ----------------

    [Required, StringLength(80)]
    [Display(Name = "Student first name")]
    public string StudentFirstName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    [Display(Name = "Student last name")]
    public string StudentLastName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "Grade")]
    public string StudentGrade { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly StudentBirthDate { get; set; }

    [StringLength(1000)]
    [Display(Name = "Medical concerns (allergies, medications, conditions, sensory)")]
    public string? MedicalConcerns { get; set; }

    [StringLength(1000)]
    [Display(Name = "Anything else Karen should know")]
    public string? StudentNotes { get; set; }

    // ---------------- Emergency ----------------

    [Required, StringLength(160)]
    [Display(Name = "Emergency contact name")]
    public string EmergencyName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "Emergency contact phone")]
    public string EmergencyPhone { get; set; } = string.Empty;

    [Required, StringLength(60)]
    [Display(Name = "Relationship to student")]
    public string EmergencyRelationship { get; set; } = string.Empty;

    // ---------------- Pickup ----------------

    [Required, StringLength(500)]
    [Display(Name = "Authorized pickup names",
             Description = "Who is allowed to pick up your child? One per line or comma-separated. Include their relationship (parent, grandparent, aunt, etc).")]
    public string PickupAuthorization { get; set; } = string.Empty;

    // ---------------- Payment ----------------

    [Required]
    [Display(Name = "Payment option")]
    public ProgramPaymentType PaymentType { get; set; } = ProgramPaymentType.FullOneTime;

    // ---------------- Consent ----------------

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the waiver to enroll.")]
    [Display(Name = "I have read and accept the waiver")]
    public bool WaiverAccepted { get; set; }

    [Display(Name = "I grant Lake Country Spanish permission to photograph or video my child during the program")]
    public bool PhotoReleaseGranted { get; set; }
}

/// <summary>Thank-you page after a successful enrollment (any payment path).</summary>
public sealed class ProgramEnrollmentThankYouViewModel
{
    public EnrollmentProgram Program { get; init; } = null!;
    public ProgramEnrollment Enrollment { get; init; } = null!;

    /// <summary>Display label for the payment state, tailored to the payment type.</summary>
    public string PaymentStatusHeadline => (Enrollment.PaymentType, Enrollment.Status) switch
    {
        (ProgramPaymentType.FullOneTime, ProgramEnrollmentStatus.FullyPaid) =>
            "Payment received — you're all set!",

        (ProgramPaymentType.TwoInstallment, ProgramEnrollmentStatus.FirstPaymentComplete) =>
            "First installment received — you're enrolled!",

        (ProgramPaymentType.TwoInstallment, ProgramEnrollmentStatus.FullyPaid) =>
            "Both installments received — you're all set!",

        (ProgramPaymentType.CashInHand, _) =>
            "Registration received — please pay Karen at the booth to confirm.",

        _ => "Registration received. Your payment is still processing — you'll get an email once it clears."
    };
}
