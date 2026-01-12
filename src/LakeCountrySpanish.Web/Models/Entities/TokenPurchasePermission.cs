namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Reason for granting token purchase permission.
/// </summary>
public enum PermissionReason
{
    /// <summary>
    /// Trial tokens for new students to try the service.
    /// </summary>
    Trial,

    /// <summary>
    /// Additional tokens for existing students/subscribers.
    /// </summary>
    Additional,

    /// <summary>
    /// Administrative grant (special circumstances).
    /// </summary>
    Admin
}

/// <summary>
/// Represents Karen's permission for a student to purchase tokens.
/// Without active permission, students cannot purchase tokens.
/// </summary>
public class TokenPurchasePermission
{
    public int Id { get; set; }

    /// <summary>
    /// Student who has this permission.
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this permission is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of tokens the student can purchase under this permission.
    /// </summary>
    public int TokenLimit { get; set; }

    /// <summary>
    /// Number of tokens already purchased under this permission.
    /// </summary>
    public int TokensPurchased { get; set; } = 0;

    /// <summary>
    /// When this permission expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// How long purchased tokens are valid after purchase (in days).
    /// </summary>
    public int TokenValidityDays { get; set; } = 30;

    /// <summary>
    /// Reason for granting this permission.
    /// </summary>
    public PermissionReason Reason { get; set; }

    /// <summary>
    /// Admin notes about this permission.
    /// </summary>
    public string? AdminNotes { get; set; }

    /// <summary>
    /// Admin who granted this permission.
    /// </summary>
    public string? GrantedById { get; set; }

    /// <summary>
    /// When this permission was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this permission was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Price per token in dollars.
    /// </summary>
    public decimal TokenPrice { get; set; } = 30.00m;

    /// <summary>
    /// Remaining tokens available for purchase.
    /// </summary>
    public int TokensRemaining => TokenLimit - TokensPurchased;

    /// <summary>
    /// Whether this permission is still active (enabled, not expired, has remaining tokens).
    /// </summary>
    public bool IsActive => IsEnabled &&
                            ExpiresAt > DateTime.UtcNow &&
                            TokensRemaining > 0;

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual ApplicationUser? GrantedBy { get; set; }
    public virtual ICollection<Token> Tokens { get; set; } = new List<Token>();
}
