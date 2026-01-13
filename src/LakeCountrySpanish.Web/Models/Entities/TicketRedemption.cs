namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// How a ticket (free class reward) was redeemed.
/// </summary>
public enum TicketRedemptionType
{
    /// <summary>
    /// Used to schedule an extra free class.
    /// </summary>
    FreeExtraClass,

    /// <summary>
    /// Applied as a discount to an invoice/payment.
    /// </summary>
    InvoiceDiscount
}

/// <summary>
/// Tracks how a ticket (free class reward) was redeemed.
/// Allows flexibility for students to choose how to use their rewards.
/// </summary>
public class TicketRedemption
{
    public int Id { get; set; }

    /// <summary>
    /// The ticket that was redeemed.
    /// </summary>
    public int TicketId { get; set; }

    /// <summary>
    /// Student who redeemed the ticket.
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// How the ticket was redeemed.
    /// </summary>
    public TicketRedemptionType Type { get; set; }

    /// <summary>
    /// The class this ticket was used for (if FreeExtraClass).
    /// </summary>
    public int? ScheduledClassId { get; set; }

    /// <summary>
    /// The payment this ticket was applied to (if InvoiceDiscount).
    /// </summary>
    public int? PaymentId { get; set; }

    /// <summary>
    /// The discount amount applied (for InvoiceDiscount type).
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// When the ticket was redeemed.
    /// </summary>
    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional notes about the redemption.
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Ticket Ticket { get; set; } = null!;
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual ScheduledClass? ScheduledClass { get; set; }
    public virtual Payment? Payment { get; set; }
}
