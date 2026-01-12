namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Source of a token - how it was acquired.
/// </summary>
public enum TokenSource
{
    /// <summary>
    /// Earned through completing exercises and earning points (100 points = 1 token).
    /// These tokens never expire.
    /// </summary>
    Earned,

    /// <summary>
    /// Purchased with Karen's permission.
    /// These tokens have expiration dates.
    /// </summary>
    Purchased,

    /// <summary>
    /// Granted directly by admin (e.g., compensation, promotion).
    /// May or may not have expiration.
    /// </summary>
    AdminGrant
}

/// <summary>
/// Represents a token or batch of tokens owned by a student.
/// Tokens can be used to book classes.
/// </summary>
public class Token
{
    public int Id { get; set; }

    /// <summary>
    /// Student who owns this token.
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// How this token was acquired.
    /// </summary>
    public TokenSource Source { get; set; }

    /// <summary>
    /// Original quantity of tokens in this batch.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Remaining usable tokens in this batch.
    /// </summary>
    public int QuantityRemaining { get; set; }

    /// <summary>
    /// When these tokens expire. Null means never expires (earned tokens).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Reference to the permission that allowed purchase (if Source is Purchased).
    /// </summary>
    public int? TokenPurchasePermissionId { get; set; }

    /// <summary>
    /// Stripe Payment Intent ID if purchased.
    /// </summary>
    public string? StripePaymentIntentId { get; set; }

    /// <summary>
    /// Stripe Session ID if purchased.
    /// </summary>
    public string? StripeSessionId { get; set; }

    /// <summary>
    /// Admin notes (for AdminGrant tokens).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// When this token batch was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this token batch is still active (not fully used or expired).
    /// </summary>
    public bool IsActive => QuantityRemaining > 0 && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual TokenPurchasePermission? TokenPurchasePermission { get; set; }
    public virtual ICollection<TokenTransaction> Transactions { get; set; } = new List<TokenTransaction>();
}
