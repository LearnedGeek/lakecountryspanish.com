namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Category of badge.
/// </summary>
public enum BadgeCategory
{
    /// <summary>
    /// Milestone badges (First 100 Points, 500 Points, etc.)
    /// </summary>
    Milestone,

    /// <summary>
    /// Topic mastery badges (Preterite Master, Subjunctive Survivor, etc.)
    /// </summary>
    TopicMastery,

    /// <summary>
    /// Consistency badges (7-Day Streak, 30-Day Streak, etc.)
    /// </summary>
    Consistency,

    /// <summary>
    /// Challenge badges (Perfect Score, Speed Round, etc.)
    /// </summary>
    Challenge,

    /// <summary>
    /// Level progress badges (A1 Graduate, B1 Warrior, etc.)
    /// </summary>
    LevelProgress,

    /// <summary>
    /// Special/event badges.
    /// </summary>
    Special
}

/// <summary>
/// Type of requirement for earning a badge.
/// </summary>
public enum BadgeRequirementType
{
    /// <summary>
    /// Reach a certain point total.
    /// </summary>
    Points,

    /// <summary>
    /// Maintain a streak for X days.
    /// </summary>
    Streak,

    /// <summary>
    /// Complete X classes.
    /// </summary>
    ClassesCompleted,

    /// <summary>
    /// Achieve perfect score on X assignments.
    /// </summary>
    PerfectScores,

    /// <summary>
    /// Complete X assignments in a topic.
    /// </summary>
    TopicAssignments,

    /// <summary>
    /// Reach a CEFR level.
    /// </summary>
    CefrLevel,

    /// <summary>
    /// Custom requirement (checked manually or via special logic).
    /// </summary>
    Custom
}

/// <summary>
/// Defines a badge that students can earn.
/// </summary>
public class Badge
{
    public int Id { get; set; }

    /// <summary>
    /// Badge name displayed to users.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of how to earn this badge.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Icon URL or CSS class for display.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Emoji to display for this badge.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    /// Category of this badge.
    /// </summary>
    public BadgeCategory Category { get; set; }

    /// <summary>
    /// Type of requirement to earn this badge.
    /// </summary>
    public BadgeRequirementType RequirementType { get; set; }

    /// <summary>
    /// Numeric value for the requirement (e.g., 100 for 100 points).
    /// </summary>
    public int RequirementValue { get; set; }

    /// <summary>
    /// Additional requirement context (e.g., topic name for TopicAssignments).
    /// </summary>
    public string? RequirementContext { get; set; }

    /// <summary>
    /// Bonus points awarded when earning this badge.
    /// </summary>
    public int BonusPoints { get; set; } = 0;

    /// <summary>
    /// Whether this badge is currently active and earnable.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order for badge lists.
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// When this badge was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<StudentBadge> StudentBadges { get; set; } = new List<StudentBadge>();
    public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
}
