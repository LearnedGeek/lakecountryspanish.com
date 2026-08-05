using LakeCountrySpanish.Web.Models.Entities;
using Stripe;
using Stripe.Checkout;

namespace LakeCountrySpanish.Web.Services.Programs;

/// <summary>
/// Handles the parent-facing enrollment lifecycle for an
/// <see cref="EnrollmentProgram"/>: form submission → Stripe checkout (or
/// cash short-circuit), plus the webhook-side status transitions when Stripe
/// reports payment events.
/// </summary>
public interface IProgramEnrollmentService
{
    /// <summary>
    /// Persist a new enrollment row and (for the two Stripe payment paths)
    /// create the Stripe Checkout Session. Returns a discriminated result: a
    /// redirect URL for the Stripe paths, or a bare success flag for the
    /// cash-in-hand path (nothing to redirect to, caller shows thank-you).
    /// </summary>
    Task<EnrollmentSubmissionResult> SubmitAsync(EnrollmentSubmission input, CancellationToken ct = default);

    /// <summary>Fetch enrollment by id, including the parent Program.</summary>
    Task<ProgramEnrollment?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Fetch enrollment by Stripe Checkout Session id (used by the thank-you page).</summary>
    Task<ProgramEnrollment?> GetByCheckoutSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Webhook entry point for <c>checkout.session.completed</c> events that carry
    /// an <c>lcs.enrollmentId</c> metadata field. Marks the first payment received
    /// and, for subscription-mode sessions, records the Stripe subscription id.
    /// Returns false when the session doesn't belong to any enrollment (caller
    /// falls through to the normal payment-service flow).
    /// </summary>
    Task<bool> HandleCheckoutSessionCompletedAsync(Session session, CancellationToken ct = default);

    /// <summary>
    /// Webhook entry point for <c>invoice.paid</c> events on installment subscriptions.
    /// Bumps status to <see cref="ProgramEnrollmentStatus.FullyPaid"/> when the second
    /// installment lands. Returns false when the invoice isn't for an enrollment
    /// subscription (caller falls through).
    /// </summary>
    Task<bool> HandleInvoicePaidAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Admin action: mark a <see cref="ProgramPaymentType.CashInHand"/> enrollment
    /// as paid once the cash / check is in hand. Writes an audit event capturing
    /// the actor and timestamp so the co-founders can see who did what.
    /// </summary>
    Task<ProgramEnrollment> MarkCashConfirmedAsync(int enrollmentId, AdminActor actor, CancellationToken ct = default);

    /// <summary>
    /// Admin action: reverse a prior cash confirmation (mistake, refunded, etc.).
    /// Flips status back to CashPending, zeros TotalAmountPaid, writes an audit
    /// event with the reason so the reversal is visible in the trail alongside
    /// the original confirmation.
    /// </summary>
    Task<ProgramEnrollment> UndoCashConfirmationAsync(int enrollmentId, AdminActor actor, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Full audit trail for one enrollment, oldest first. Used to render the
    /// "who did what and when" view on the admin roster.
    /// </summary>
    Task<IReadOnlyList<ProgramEnrollmentAuditEvent>> GetAuditEventsAsync(int enrollmentId, CancellationToken ct = default);
}

/// <summary>
/// Compact identity for an admin performing an audited action. Captures both
/// the AspNetUsers.Id (for referential lookup) and a display label frozen at
/// action time (so the audit trail reads correctly even if the user is later
/// renamed or removed).
/// </summary>
public sealed record AdminActor(string? UserId, string DisplayName)
{
    public static AdminActor System { get; } = new(null, "system");
}

/// <summary>Everything the parent-facing form + waiver collects.</summary>
public sealed class EnrollmentSubmission
{
    public int ProgramId { get; init; }
    public ProgramPaymentType PaymentType { get; init; }

    public string ParentFirstName { get; init; } = string.Empty;
    public string ParentLastName { get; init; } = string.Empty;
    public string ParentEmail { get; init; } = string.Empty;
    public string ParentPhone { get; init; } = string.Empty;
    public string ParentAddressLine1 { get; init; } = string.Empty;
    public string ParentCity { get; init; } = string.Empty;
    public string ParentState { get; init; } = string.Empty;
    public string ParentZip { get; init; } = string.Empty;

    public string StudentFirstName { get; init; } = string.Empty;
    public string StudentLastName { get; init; } = string.Empty;
    public string StudentGrade { get; init; } = string.Empty;
    public DateOnly StudentBirthDate { get; init; }
    public string? MedicalConcerns { get; init; }
    public string? StudentNotes { get; init; }

    public string EmergencyName { get; init; } = string.Empty;
    public string EmergencyPhone { get; init; } = string.Empty;
    public string EmergencyRelationship { get; init; } = string.Empty;

    public string PickupAuthorization { get; init; } = string.Empty;

    /// <summary>True when the parent checked the waiver box. Must be true to enroll.</summary>
    public bool WaiverAccepted { get; init; }

    /// <summary>True when the parent granted the (optional) photo release.</summary>
    public bool PhotoReleaseGranted { get; init; }

    /// <summary>Base URL for Stripe redirect building (e.g. https://lakecountryspanish.com).</summary>
    public string BaseUrl { get; init; } = string.Empty;
}

public sealed class EnrollmentSubmissionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Populated for Stripe payment paths — where to redirect the browser next.</summary>
    public string? StripeCheckoutUrl { get; init; }

    /// <summary>The persisted enrollment (regardless of payment path).</summary>
    public int EnrollmentId { get; init; }

    /// <summary>True when payment path is cash-in-hand — controller should show a thank-you page directly.</summary>
    public bool IsCashInHand { get; init; }

    public static EnrollmentSubmissionResult ForStripe(int id, string url) =>
        new() { Success = true, EnrollmentId = id, StripeCheckoutUrl = url };

    public static EnrollmentSubmissionResult ForCash(int id) =>
        new() { Success = true, EnrollmentId = id, IsCashInHand = true };

    public static EnrollmentSubmissionResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };
}
