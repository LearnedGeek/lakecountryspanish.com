using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service interface for managing points, badges, and streaks.
/// </summary>
public interface IGamificationService
{
    // Points Management

    /// <summary>
    /// Award points to a student with automatic multiplier calculation.
    /// Also checks for badge eligibility and token conversion.
    /// </summary>
    Task<PointTransaction> AwardPointsAsync(
        string studentId,
        int basePoints,
        PointSource source,
        int? scheduledClassId = null,
        string? details = null);

    /// <summary>
    /// Get the points multiplier for a student (1.5x for subscribers).
    /// </summary>
    Task<decimal> GetPointsMultiplierAsync(string studentId);

    /// <summary>
    /// Get a student's total points.
    /// </summary>
    Task<int> GetTotalPointsAsync(string studentId);

    /// <summary>
    /// Get point transaction history for a student.
    /// </summary>
    Task<IEnumerable<PointTransaction>> GetPointHistoryAsync(string studentId, int? limit = null);

    /// <summary>
    /// Manually adjust a student's points (admin function).
    /// </summary>
    Task<PointTransaction> AdjustPointsAsync(string studentId, int adjustment, string reason, string adminId);

    // Streak Management

    /// <summary>
    /// Get or create a student's streak record.
    /// </summary>
    Task<StudentStreak> GetStreakAsync(string studentId);

    /// <summary>
    /// Record activity and update streak. Call this when student completes
    /// an assignment, attends a class, or performs any qualifying activity.
    /// </summary>
    Task<StudentStreak> RecordActivityAsync(string studentId);

    /// <summary>
    /// Check and reset expired streaks (should be called periodically).
    /// </summary>
    Task<int> ResetExpiredStreaksAsync();

    // Badge Management

    /// <summary>
    /// Get all active badges.
    /// </summary>
    Task<IEnumerable<Badge>> GetAllBadgesAsync();

    /// <summary>
    /// Get badges by category.
    /// </summary>
    Task<IEnumerable<Badge>> GetBadgesByCategoryAsync(BadgeCategory category);

    /// <summary>
    /// Get a student's earned badges.
    /// </summary>
    Task<IEnumerable<StudentBadge>> GetStudentBadgesAsync(string studentId);

    /// <summary>
    /// Get unviewed/new badges for a student.
    /// </summary>
    Task<IEnumerable<StudentBadge>> GetUnviewedBadgesAsync(string studentId);

    /// <summary>
    /// Mark badges as viewed.
    /// </summary>
    Task MarkBadgesAsViewedAsync(string studentId);

    /// <summary>
    /// Check if student has earned a specific badge.
    /// </summary>
    Task<bool> HasBadgeAsync(string studentId, int badgeId);

    /// <summary>
    /// Check and award any eligible badges for a student.
    /// Called automatically after point awards.
    /// </summary>
    Task<IEnumerable<StudentBadge>> CheckAndAwardBadgesAsync(string studentId);

    /// <summary>
    /// Manually award a badge to a student (admin function).
    /// </summary>
    Task<StudentBadge?> AwardBadgeAsync(string studentId, int badgeId, string? context = null);

    // Token Conversion

    /// <summary>
    /// Check if student has enough points for token conversion (100 points = 1 token).
    /// If so, automatically convert and award the token.
    /// Called automatically after point awards.
    /// </summary>
    Task<int> CheckAndConvertPointsToTokensAsync(string studentId);

    // Progress Summary

    /// <summary>
    /// Get a summary of student's gamification progress.
    /// </summary>
    Task<GamificationProgress> GetProgressAsync(string studentId);

    // Admin Badge Management

    /// <summary>
    /// Create a new badge.
    /// </summary>
    Task<Badge> CreateBadgeAsync(Badge badge);

    /// <summary>
    /// Update an existing badge.
    /// </summary>
    Task<Badge?> UpdateBadgeAsync(Badge badge);

    /// <summary>
    /// Disable a badge (students who earned it keep it).
    /// </summary>
    Task<bool> DisableBadgeAsync(int badgeId);
}

/// <summary>
/// Summary of a student's gamification progress.
/// </summary>
public class GamificationProgress
{
    public int TotalPoints { get; set; }
    public int PointsTowardsNextToken { get; set; }
    public int TokensEarnedFromPoints { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool WasActiveToday { get; set; }
    public int TotalBadgesEarned { get; set; }
    public int UnviewedBadgeCount { get; set; }
    public decimal PointsMultiplier { get; set; }
    public IEnumerable<StudentBadge> RecentBadges { get; set; } = new List<StudentBadge>();
    public IEnumerable<Badge> NextEarnableBadges { get; set; } = new List<Badge>();
}
