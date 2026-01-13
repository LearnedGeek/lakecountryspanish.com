using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// View model for the Token Store page where students can spend tokens on tickets.
/// </summary>
public class TokenStoreViewModel
{
    // Token Balance
    public int TotalTokens { get; set; }
    public int EarnedTokens { get; set; }

    // Ticket Balance
    public int AvailableTickets { get; set; }
    public Dictionary<TicketSource, int> TicketsBySource { get; set; } = new();

    // Token Store Item
    public int TicketTokenCost { get; set; }
    public bool CanAffordTicket => TotalTokens >= TicketTokenCost;

    // Points Progress
    public int CurrentPoints { get; set; }
    public int PointsToNextToken => 100 - (CurrentPoints % 100);
    public int PointProgressPercent => CurrentPoints % 100;

    // Ticket Purchase Permission (for buying tickets with money)
    public bool CanPurchaseTickets { get; set; }
    public TokenPurchasePermission? PurchasePermission { get; set; }

    // Recent Transactions
    public IEnumerable<TokenTransaction> RecentTokenTransactions { get; set; } = new List<TokenTransaction>();
    public IEnumerable<Ticket> RecentTickets { get; set; } = new List<Ticket>();

    // Messages
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}
