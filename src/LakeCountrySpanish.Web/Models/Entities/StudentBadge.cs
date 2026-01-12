namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Represents a badge earned by a student.
/// </summary>
public class StudentBadge
{
    public int Id { get; set; }

    /// <summary>
    /// Student who earned this badge.
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// The badge that was earned.
    /// </summary>
    public int BadgeId { get; set; }

    /// <summary>
    /// When this badge was earned.
    /// </summary>
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Context about how/why badge was earned (e.g., "Reached 100 points").
    /// </summary>
    public string? EarnedContext { get; set; }

    /// <summary>
    /// Whether this badge has been viewed/acknowledged by the student.
    /// </summary>
    public bool IsViewed { get; set; } = false;

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual Badge Badge { get; set; } = null!;
}
