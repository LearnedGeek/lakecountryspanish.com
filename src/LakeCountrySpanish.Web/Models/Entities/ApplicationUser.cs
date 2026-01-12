using Microsoft.AspNetCore.Identity;

namespace LakeCountrySpanish.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public decimal? CustomHourlyRate { get; set; }
    public string? ClassroomUrl { get; set; }  // Default Zoom/Meet link for this student
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;  // Force password change on first login

    // Stripe integration
    /// <summary>
    /// Stripe Customer ID for subscription and portal access
    /// </summary>
    public string? StripeCustomerId { get; set; }

    // CEFR Level for curriculum
    /// <summary>
    /// Student's current CEFR level (A1, A2, B1, B2, C1, C2)
    /// </summary>
    public string CefrLevel { get; set; } = "A1";

    // Gamification (Phase 3)
    /// <summary>
    /// Total accumulated points
    /// </summary>
    public int TotalPoints { get; set; } = 0;

    /// <summary>
    /// Points progress towards next token (0-99)
    /// </summary>
    public int PointsTowardsNextToken => TotalPoints % 100;

    /// <summary>
    /// Total tokens earned from points
    /// </summary>
    public int TokensEarnedFromPoints => TotalPoints / 100;

    public string FullName => $"{FirstName} {LastName}";

    // Navigation properties
    public virtual ICollection<ScheduledClass> ScheduledClasses { get; set; } = new List<ScheduledClass>();
    public virtual ICollection<StudentPackage> StudentPackages { get; set; } = new List<StudentPackage>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<StudentDocument> StudentDocuments { get; set; } = new List<StudentDocument>();
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    // Token navigation properties
    public virtual ICollection<Token> Tokens { get; set; } = new List<Token>();
    public virtual ICollection<TokenPurchasePermission> TokenPurchasePermissions { get; set; } = new List<TokenPurchasePermission>();
    public virtual ICollection<TokenTransaction> TokenTransactions { get; set; } = new List<TokenTransaction>();

    // Gamification navigation properties
    public virtual ICollection<StudentBadge> StudentBadges { get; set; } = new List<StudentBadge>();
    public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
}
