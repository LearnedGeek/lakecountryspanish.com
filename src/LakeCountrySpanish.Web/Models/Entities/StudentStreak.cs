namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Tracks a student's activity streak.
/// </summary>
public class StudentStreak
{
    public int Id { get; set; }

    /// <summary>
    /// Student whose streak this tracks.
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// Current consecutive days of activity.
    /// </summary>
    public int CurrentStreak { get; set; } = 0;

    /// <summary>
    /// Longest streak ever achieved.
    /// </summary>
    public int LongestStreak { get; set; } = 0;

    /// <summary>
    /// Date of last qualifying activity.
    /// </summary>
    public DateTime? LastActivityDate { get; set; }

    /// <summary>
    /// Total number of active days (not necessarily consecutive).
    /// </summary>
    public int TotalActiveDays { get; set; } = 0;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Check if streak is still active (activity yesterday or today).
    /// </summary>
    public bool IsStreakActive
    {
        get
        {
            if (!LastActivityDate.HasValue) return false;
            var today = DateTime.UtcNow.Date;
            var lastActivity = LastActivityDate.Value.Date;
            return lastActivity >= today.AddDays(-1);
        }
    }

    /// <summary>
    /// Check if student has been active today.
    /// </summary>
    public bool WasActiveToday
    {
        get
        {
            if (!LastActivityDate.HasValue) return false;
            return LastActivityDate.Value.Date == DateTime.UtcNow.Date;
        }
    }

    // Navigation property
    public virtual ApplicationUser Student { get; set; } = null!;
}
