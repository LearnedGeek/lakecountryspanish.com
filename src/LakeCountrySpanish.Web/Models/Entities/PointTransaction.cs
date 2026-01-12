namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Source of points - how they were earned.
/// </summary>
public enum PointSource
{
    /// <summary>
    /// Points from completing an exercise/assignment.
    /// </summary>
    Exercise,

    /// <summary>
    /// Bonus points for perfect score on assignment.
    /// </summary>
    PerfectScore,

    /// <summary>
    /// Bonus points for first-attempt correct answer.
    /// </summary>
    FirstAttemptBonus,

    /// <summary>
    /// Points for completing a class.
    /// </summary>
    ClassCompletion,

    /// <summary>
    /// Streak bonus points.
    /// </summary>
    StreakBonus,

    /// <summary>
    /// Points from earning a badge.
    /// </summary>
    BadgeEarned,

    /// <summary>
    /// Admin-granted points (compensation, promotion).
    /// </summary>
    AdminGrant,

    /// <summary>
    /// Adjustment (correction, removal).
    /// </summary>
    Adjustment
}

/// <summary>
/// Audit trail for all point awards.
/// </summary>
public class PointTransaction
{
    public int Id { get; set; }

    /// <summary>
    /// Student who earned/lost these points.
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// How the points were earned.
    /// </summary>
    public PointSource Source { get; set; }

    /// <summary>
    /// Base points before multiplier.
    /// </summary>
    public int BasePoints { get; set; }

    /// <summary>
    /// Multiplier applied (e.g., 1.5x for subscribers).
    /// </summary>
    public decimal Multiplier { get; set; } = 1.0m;

    /// <summary>
    /// Final points awarded after multiplier.
    /// </summary>
    public int FinalPoints { get; set; }

    /// <summary>
    /// Student's total point balance after this transaction.
    /// </summary>
    public int BalanceAfter { get; set; }

    /// <summary>
    /// Reference to scheduled class (if ClassCompletion).
    /// </summary>
    public int? ScheduledClassId { get; set; }

    /// <summary>
    /// Reference to badge earned (if BadgeEarned).
    /// </summary>
    public int? BadgeId { get; set; }

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
    public virtual ScheduledClass? ScheduledClass { get; set; }
    public virtual Badge? Badge { get; set; }
}
