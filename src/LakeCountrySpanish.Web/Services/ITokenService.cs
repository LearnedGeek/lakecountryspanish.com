using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service interface for managing tokens and token purchase permissions.
/// </summary>
public interface ITokenService
{
    // Token balance queries

    /// <summary>
    /// Get the total balance of earned tokens (never expire) for a student.
    /// </summary>
    Task<int> GetEarnedTokenBalanceAsync(string studentId);

    /// <summary>
    /// Get the total balance of purchased tokens (may have expiration) for a student.
    /// </summary>
    Task<int> GetPurchasedTokenBalanceAsync(string studentId);

    /// <summary>
    /// Get the total usable token balance for a student (earned + active purchased).
    /// </summary>
    Task<int> GetTotalTokenBalanceAsync(string studentId);

    /// <summary>
    /// Get all active token batches for a student.
    /// </summary>
    Task<IEnumerable<Token>> GetActiveTokensAsync(string studentId);

    // Token purchase permission

    /// <summary>
    /// Get the active purchase permission for a student, if any.
    /// </summary>
    Task<TokenPurchasePermission?> GetActivePermissionAsync(string studentId);

    /// <summary>
    /// Get all purchase permissions for a student (including expired).
    /// </summary>
    Task<IEnumerable<TokenPurchasePermission>> GetPermissionHistoryAsync(string studentId);

    /// <summary>
    /// Check if a student can currently purchase tokens.
    /// </summary>
    Task<bool> CanPurchaseTokensAsync(string studentId);

    // Token purchase

    /// <summary>
    /// Create a Stripe checkout session for token purchase.
    /// Returns the checkout session URL or null if permission not active.
    /// </summary>
    Task<string?> CreateTokenPurchaseCheckoutAsync(string studentId, int quantity, string successUrl, string cancelUrl);

    /// <summary>
    /// Complete a token purchase after successful Stripe payment.
    /// Called from webhook handler.
    /// </summary>
    Task<Token?> CompletePurchaseAsync(string studentId, int quantity, string stripeSessionId, string? paymentIntentId);

    // Token usage

    /// <summary>
    /// Use a token to book a class. Consumes from oldest expiring tokens first.
    /// Returns true if successful, false if insufficient balance.
    /// </summary>
    Task<bool> UseTokenForClassAsync(string studentId, int scheduledClassId);

    /// <summary>
    /// Refund a token when a class is cancelled with proper notice.
    /// </summary>
    Task<bool> RefundTokenAsync(string studentId, int scheduledClassId);

    /// <summary>
    /// Forfeit a token when a class is cancelled without proper notice.
    /// </summary>
    Task<bool> ForfeitTokenAsync(string studentId, int scheduledClassId);

    // Token earning (from points)

    /// <summary>
    /// Award earned tokens to a student (from points conversion or other means).
    /// </summary>
    Task<Token> AwardEarnedTokensAsync(string studentId, int quantity, string details);

    // Admin functions

    /// <summary>
    /// Grant purchase permission to a student.
    /// </summary>
    Task<TokenPurchasePermission> GrantPurchasePermissionAsync(
        string studentId,
        int tokenLimit,
        DateTime expiresAt,
        PermissionReason reason,
        int tokenValidityDays,
        decimal tokenPrice,
        string adminId,
        string? notes = null);

    /// <summary>
    /// Update an existing purchase permission.
    /// </summary>
    Task<TokenPurchasePermission?> UpdatePurchasePermissionAsync(
        int permissionId,
        bool isEnabled,
        int? tokenLimit = null,
        DateTime? expiresAt = null,
        string? notes = null);

    /// <summary>
    /// Disable a purchase permission.
    /// </summary>
    Task<bool> DisablePermissionAsync(int permissionId);

    /// <summary>
    /// Directly grant tokens to a student (admin compensation, promotion, etc.).
    /// </summary>
    Task<Token> GrantTokensAsync(string studentId, int quantity, DateTime? expiresAt, string adminId, string? notes = null);

    // Transaction history

    /// <summary>
    /// Get token transaction history for a student.
    /// </summary>
    Task<IEnumerable<TokenTransaction>> GetTransactionHistoryAsync(string studentId, int? limit = null);

    // Expiration handling

    /// <summary>
    /// Expire all tokens that have passed their expiration date.
    /// Should be called periodically (e.g., daily job).
    /// </summary>
    Task<int> ExpireTokensAsync();
}
