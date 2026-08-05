namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// One immutable audit record for a <see cref="ProgramEnrollment"/>. Written
/// whenever an admin acts on an enrollment in a way that touches money or
/// status — cash confirmed, cash reversed, refund posted, etc.
///
/// Purpose: co-founder transparency. Karen and Cece both need to see who did
/// what and when, without having to spelunk through free-form admin notes.
/// This table is append-only from application code; if a mistake is made, the
/// correction is a *new* audit event (e.g. CashConfirmationUndone) rather than
/// an in-place edit of the previous one.
/// </summary>
public class ProgramEnrollmentAuditEvent
{
    public int Id { get; set; }

    public int EnrollmentId { get; set; }
    public virtual ProgramEnrollment Enrollment { get; set; } = null!;

    /// <summary>When the action happened (server UTC).</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// AspNetUsers.Id of the admin who acted. Nullable because system-triggered
    /// events (Stripe webhook) don't have an interactive admin.
    /// </summary>
    public string? ActorUserId { get; set; }

    /// <summary>
    /// Display label for the actor at the moment of the event (username / email /
    /// "system"). Denormalized so the audit log still reads correctly even if the
    /// user is later renamed or deleted.
    /// </summary>
    public string ActorDisplayName { get; set; } = string.Empty;

    public EnrollmentAuditEventType EventType { get; set; }

    /// <summary>Optional free-text detail. E.g. "Cash received in twenties" or a Stripe error message.</summary>
    public string? Details { get; set; }

    /// <summary>
    /// Change in TotalAmountPaid this event represents. +$10.00 for a cash
    /// confirmation, -$10.00 for a reversal. Zero for status-only events.
    /// </summary>
    public decimal MonetaryDelta { get; set; }
}

/// <summary>Type of admin action captured by an <see cref="ProgramEnrollmentAuditEvent"/>.</summary>
public enum EnrollmentAuditEventType
{
    /// <summary>Admin marked a cash-in-hand enrollment as received.</summary>
    CashConfirmed = 0,

    /// <summary>Admin reversed a prior CashConfirmed event (e.g. mistake, refunded).</summary>
    CashConfirmationUndone = 1,

    /// <summary>Admin left a note on the enrollment (no status/money change).</summary>
    NoteAdded = 2,

    /// <summary>Admin cancelled the enrollment manually.</summary>
    Cancelled = 3,

    /// <summary>Admin marked the enrollment as refunded.</summary>
    Refunded = 4
}
