namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// How a ticket was acquired.
/// </summary>
public enum TicketSource
{
    /// <summary>
    /// Purchased with money (requires admin permission).
    /// </summary>
    Purchased,

    /// <summary>
    /// Bought with tokens in the Token Store.
    /// </summary>
    TokenExchange,

    /// <summary>
    /// Granted directly by admin (comp, promotion, etc.).
    /// </summary>
    AdminGrant,

    /// <summary>
    /// Granted by monthly subscription.
    /// </summary>
    Subscription,

    /// <summary>
    /// Migrated from legacy StudentPackage credits.
    /// </summary>
    LegacyMigration
}

/// <summary>
/// Represents a class ticket - the currency for booking classes.
/// 1 ticket = 1 class.
/// </summary>
public class Ticket
{
    public int Id { get; set; }

    /// <summary>
    /// Student who owns this ticket.
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// How this ticket was acquired.
    /// </summary>
    public TicketSource Source { get; set; }

    /// <summary>
    /// When this ticket was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this ticket expires. Null means never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether this ticket has been used.
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// The class this ticket was used for (if used).
    /// </summary>
    public int? UsedForClassId { get; set; }

    /// <summary>
    /// When this ticket was used.
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// Reference to the Stripe session if purchased with money.
    /// </summary>
    public string? StripeSessionId { get; set; }

    /// <summary>
    /// Reference to the Stripe payment intent if purchased with money.
    /// </summary>
    public string? StripePaymentIntentId { get; set; }

    /// <summary>
    /// Reference to the subscription that granted this ticket.
    /// </summary>
    public int? SubscriptionId { get; set; }

    /// <summary>
    /// Number of tokens spent to acquire this ticket (if TokenExchange).
    /// </summary>
    public int? TokensSpent { get; set; }

    /// <summary>
    /// Admin notes (for grants/adjustments).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this ticket is available for use.
    /// </summary>
    public bool IsAvailable => !IsUsed && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual ScheduledClass? UsedForClass { get; set; }
    public virtual Subscription? Subscription { get; set; }
}
