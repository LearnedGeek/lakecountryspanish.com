namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Type of token transaction.
/// </summary>
public enum TokenTransactionType
{
    /// <summary>
    /// Tokens earned from points (100 points = 1 token).
    /// </summary>
    Earned,

    /// <summary>
    /// Tokens purchased with permission.
    /// </summary>
    Purchased,

    /// <summary>
    /// Tokens granted by admin.
    /// </summary>
    AdminGrant,

    /// <summary>
    /// Token used to book a class.
    /// </summary>
    UsedForClass,

    /// <summary>
    /// Token expired (purchased tokens only).
    /// </summary>
    Expired,

    /// <summary>
    /// Token refunded (class cancelled with notice).
    /// </summary>
    Refunded,

    /// <summary>
    /// Token forfeited (class cancelled without notice).
    /// </summary>
    Forfeited
}

/// <summary>
/// Audit trail for all token-related transactions.
/// </summary>
public class TokenTransaction
{
    public int Id { get; set; }

    /// <summary>
    /// Student involved in this transaction.
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// Type of transaction.
    /// </summary>
    public TokenTransactionType Type { get; set; }

    /// <summary>
    /// Quantity change (positive for additions, negative for uses/expirations).
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Student's total token balance after this transaction.
    /// </summary>
    public int BalanceAfter { get; set; }

    /// <summary>
    /// Reference to the token batch affected (if applicable).
    /// </summary>
    public int? TokenId { get; set; }

    /// <summary>
    /// Reference to the scheduled class (if UsedForClass/Refunded/Forfeited).
    /// </summary>
    public int? ScheduledClassId { get; set; }

    /// <summary>
    /// Additional details about the transaction.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When this transaction occurred.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual Token? Token { get; set; }
    public virtual ScheduledClass? ScheduledClass { get; set; }
}
