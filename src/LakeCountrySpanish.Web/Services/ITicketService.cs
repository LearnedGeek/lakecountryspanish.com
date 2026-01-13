using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service interface for managing class tickets.
/// Tickets are the currency for booking classes (1 ticket = 1 class).
/// </summary>
public interface ITicketService
{
    // Ticket balance queries

    /// <summary>
    /// Get the count of available tickets for a student.
    /// </summary>
    Task<int> GetAvailableTicketCountAsync(string studentId);

    /// <summary>
    /// Get all available (unused, non-expired) tickets for a student.
    /// </summary>
    Task<IEnumerable<Ticket>> GetAvailableTicketsAsync(string studentId);

    /// <summary>
    /// Get all tickets for a student (including used and expired).
    /// </summary>
    Task<IEnumerable<Ticket>> GetAllTicketsAsync(string studentId, int? limit = null);

    /// <summary>
    /// Get ticket counts by source for a student.
    /// </summary>
    Task<Dictionary<TicketSource, int>> GetTicketCountsBySourceAsync(string studentId);

    // Ticket usage

    /// <summary>
    /// Use a ticket to book a class. Consumes oldest/soonest-expiring ticket first.
    /// Returns the used ticket or null if no tickets available.
    /// </summary>
    Task<Ticket?> UseTicketForClassAsync(string studentId, int scheduledClassId);

    /// <summary>
    /// Refund a ticket when a class is cancelled with proper notice.
    /// Restores the ticket that was used for the class.
    /// </summary>
    Task<bool> RefundTicketAsync(int scheduledClassId);

    // Ticket acquisition - Token Exchange

    /// <summary>
    /// Exchange tokens for a ticket in the Token Store.
    /// Returns the new ticket or null if insufficient tokens.
    /// </summary>
    Task<Ticket?> ExchangeTokensForTicketAsync(string studentId);

    /// <summary>
    /// Get the token cost for one ticket.
    /// </summary>
    int GetTicketTokenCost();

    // Ticket acquisition - Purchase

    /// <summary>
    /// Check if a student can purchase tickets (has active permission).
    /// </summary>
    Task<bool> CanPurchaseTicketsAsync(string studentId);

    /// <summary>
    /// Get the active ticket purchase permission for a student.
    /// </summary>
    Task<TokenPurchasePermission?> GetActiveTicketPermissionAsync(string studentId);

    /// <summary>
    /// Create a Stripe checkout session for ticket purchase.
    /// Returns the checkout session URL or null if not permitted.
    /// </summary>
    Task<string?> CreateTicketPurchaseCheckoutAsync(string studentId, int quantity, string successUrl, string cancelUrl);

    /// <summary>
    /// Complete ticket purchase after successful Stripe payment.
    /// Called from webhook handler.
    /// </summary>
    Task<IEnumerable<Ticket>> CompletePurchaseAsync(string studentId, int quantity, string stripeSessionId, string? paymentIntentId, DateTime? expiresAt = null);

    // Ticket acquisition - Admin Grant

    /// <summary>
    /// Grant tickets directly to a student (admin compensation, promotion, etc.).
    /// </summary>
    Task<IEnumerable<Ticket>> GrantTicketsAsync(string studentId, int quantity, DateTime? expiresAt, string? notes = null);

    // Ticket acquisition - Subscription

    /// <summary>
    /// Grant tickets from a subscription (monthly billing).
    /// </summary>
    Task<IEnumerable<Ticket>> GrantSubscriptionTicketsAsync(string studentId, int subscriptionId, int quantity, DateTime expiresAt);

    // Migration

    /// <summary>
    /// Migrate legacy StudentPackage credits to tickets.
    /// </summary>
    Task<int> MigrateLegacyCreditsAsync(string studentId);

    /// <summary>
    /// Migrate all students' legacy credits to tickets.
    /// </summary>
    Task<int> MigrateAllLegacyCreditsAsync();
}
