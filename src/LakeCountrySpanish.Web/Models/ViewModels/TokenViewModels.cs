using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// Main view model for the token dashboard.
/// </summary>
public class TokenViewModel
{
    public int EarnedTokens { get; set; }
    public int PurchasedTokens { get; set; }
    public int TotalTokens { get; set; }
    public bool CanPurchase { get; set; }
    public TokenPurchasePermission? ActivePermission { get; set; }
    public List<TokenBatchViewModel> TokenBatches { get; set; } = new();
    public List<TokenTransactionViewModel> RecentTransactions { get; set; } = new();
}

/// <summary>
/// View model for a token batch.
/// </summary>
public class TokenBatchViewModel
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int QuantityRemaining { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public string SourceDisplay => Source switch
    {
        "Earned" => "Earned from points",
        "Purchased" => "Purchased",
        "AdminGrant" => "Granted by admin",
        _ => Source
    };

    public bool IsExpiring => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow.AddDays(7);
    public bool NeverExpires => !ExpiresAt.HasValue;
}

/// <summary>
/// View model for a token transaction.
/// </summary>
public class TokenTransactionViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TypeDisplay => Type switch
    {
        "Earned" => "Earned",
        "Purchased" => "Purchased",
        "AdminGrant" => "Admin Grant",
        "UsedForClass" => "Used for Class",
        "Expired" => "Expired",
        "Refunded" => "Refunded",
        "Forfeited" => "Forfeited",
        _ => Type
    };

    public string TypeClass => Type switch
    {
        "Earned" or "Purchased" or "AdminGrant" or "Refunded" => "text-green-600",
        "UsedForClass" or "Expired" or "Forfeited" => "text-red-600",
        _ => "text-gray-600"
    };

    public string QuantityDisplay => Quantity > 0 ? $"+{Quantity}" : Quantity.ToString();
}

/// <summary>
/// View model for admin token permission management.
/// </summary>
public class AdminTokenPermissionViewModel
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int TokenLimit { get; set; }
    public int TokensPurchased { get; set; }
    public int TokensRemaining { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int TokenValidityDays { get; set; }
    public decimal TokenPrice { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// View model for granting a new token purchase permission.
/// </summary>
public class GrantPermissionViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public int TokenLimit { get; set; } = 3;
    public int ExpirationDays { get; set; } = 30;
    public int TokenValidityDays { get; set; } = 30;
    public decimal TokenPrice { get; set; } = 30.00m;
    public string Reason { get; set; } = "Trial";
    public string? AdminNotes { get; set; }
}

/// <summary>
/// View model for granting tokens directly.
/// </summary>
public class GrantTokensViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int? ExpirationDays { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// View model for admin student token summary.
/// </summary>
public class StudentTokenSummaryViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public int EarnedTokens { get; set; }
    public int PurchasedTokens { get; set; }
    public int TotalTokens { get; set; }
    public bool HasActivePermission { get; set; }
    public TokenPurchasePermission? ActivePermission { get; set; }
}

/// <summary>
/// Consolidated view model for admin gamification management.
/// </summary>
public class StudentGamificationViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;

    // Ticket information
    public int AvailableTickets { get; set; }
    public Dictionary<TicketSource, int> TicketsBySource { get; set; } = new();
    public IEnumerable<Ticket> RecentTickets { get; set; } = new List<Ticket>();

    // Token information
    public int EarnedTokens { get; set; }
    public int PurchasedTokens { get; set; }
    public int TotalTokens { get; set; }
    public bool HasActivePermission { get; set; }
    public TokenPurchasePermission? ActivePermission { get; set; }
    public List<TokenBatchViewModel> TokenBatches { get; set; } = new();
    public List<TokenTransactionViewModel> TokenTransactions { get; set; } = new();

    // Points and engagement
    public int TotalPoints { get; set; }
    public int PointsToNextToken => 100 - (TotalPoints % 100);
    public int PointProgressPercent => TotalPoints % 100;
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalClassesCompleted { get; set; }
    public int ClassesThisMonth { get; set; }

    // Badge information
    public List<StudentBadgeViewModel> Badges { get; set; } = new();
    public int TotalBadges => Badges.Count;
}

/// <summary>
/// View model for a student badge.
/// </summary>
public class StudentBadgeViewModel
{
    public int BadgeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime EarnedAt { get; set; }
}

/// <summary>
/// View model for granting tickets to a student.
/// </summary>
public class GrantTicketsViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int? ExpirationDays { get; set; }
    public string? Notes { get; set; }
}
