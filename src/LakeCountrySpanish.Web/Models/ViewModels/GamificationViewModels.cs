using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// View model for the student progress/gamification dashboard.
/// </summary>
public class StudentProgressViewModel
{
    public GamificationProgress Progress { get; set; } = new();
    public IEnumerable<BadgeDisplayViewModel> AllBadges { get; set; } = new List<BadgeDisplayViewModel>();
    public IEnumerable<PointTransactionViewModel> RecentPointHistory { get; set; } = new List<PointTransactionViewModel>();
}

/// <summary>
/// View model for displaying a badge with earned status.
/// </summary>
public class BadgeDisplayViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public BadgeCategory Category { get; set; }
    public BadgeRequirementType RequirementType { get; set; }
    public int RequirementValue { get; set; }
    public string? RequirementContext { get; set; }
    public int BonusPoints { get; set; }
    public int DisplayOrder { get; set; }

    // Earned status
    public bool IsEarned { get; set; }
    public DateTime? EarnedAt { get; set; }
    public bool IsNew { get; set; } // Not yet viewed

    // Progress towards earning (for unearned badges)
    public int? CurrentProgress { get; set; }
    public int? ProgressPercentage => RequirementValue > 0 && CurrentProgress.HasValue
        ? Math.Min(100, (CurrentProgress.Value * 100) / RequirementValue)
        : null;

    public string CategoryDisplayName => Category switch
    {
        BadgeCategory.Milestone => "Milestone",
        BadgeCategory.TopicMastery => "Topic Mastery",
        BadgeCategory.Consistency => "Consistency",
        BadgeCategory.Challenge => "Challenge",
        BadgeCategory.LevelProgress => "Level Progress",
        BadgeCategory.Special => "Special",
        _ => Category.ToString()
    };

    public string RequirementDescription => RequirementType switch
    {
        BadgeRequirementType.Points => $"Earn {RequirementValue:N0} points",
        BadgeRequirementType.Streak => $"Maintain a {RequirementValue}-day streak",
        BadgeRequirementType.ClassesCompleted => $"Complete {RequirementValue} classes",
        BadgeRequirementType.PerfectScores => $"Get {RequirementValue} perfect scores",
        BadgeRequirementType.TopicAssignments => $"Complete {RequirementValue} {RequirementContext ?? "topic"} assignments",
        BadgeRequirementType.CefrLevel => $"Reach CEFR level {RequirementContext ?? RequirementValue.ToString()}",
        BadgeRequirementType.Custom => Description,
        _ => Description
    };
}

/// <summary>
/// View model for displaying a point transaction.
/// </summary>
public class PointTransactionViewModel
{
    public int Id { get; set; }
    public PointSource Source { get; set; }
    public int BasePoints { get; set; }
    public decimal Multiplier { get; set; }
    public int FinalPoints { get; set; }
    public int BalanceAfter { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }

    // Related entities
    public string? ClassName { get; set; }
    public string? BadgeName { get; set; }

    public string SourceDisplayName => Source switch
    {
        PointSource.Exercise => "Exercise Completed",
        PointSource.PerfectScore => "Perfect Score",
        PointSource.FirstAttemptBonus => "First Attempt Bonus",
        PointSource.ClassCompletion => "Class Completed",
        PointSource.StreakBonus => "Streak Bonus",
        PointSource.BadgeEarned => "Badge Earned",
        PointSource.AdminGrant => "Admin Award",
        PointSource.Adjustment => "Adjustment",
        _ => Source.ToString()
    };

    public string PointsDisplay => FinalPoints >= 0 ? $"+{FinalPoints}" : FinalPoints.ToString();

    public bool HasMultiplier => Multiplier > 1.0m;
}

/// <summary>
/// View model for the streak display widget.
/// </summary>
public class StreakDisplayViewModel
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool WasActiveToday { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public bool IsAtRisk => !WasActiveToday && CurrentStreak > 0;

    public string StreakStatusMessage => WasActiveToday
        ? "Keep it up! You're on fire!"
        : CurrentStreak > 0
            ? "Don't break your streak! Complete an activity today."
            : "Start your streak by completing an activity!";
}

/// <summary>
/// View model for newly earned badges notification.
/// </summary>
public class NewBadgesViewModel
{
    public IEnumerable<BadgeDisplayViewModel> NewBadges { get; set; } = new List<BadgeDisplayViewModel>();
    public int TotalNewCount => NewBadges.Count();
}

// Admin view models

/// <summary>
/// View model for admin badge management list.
/// </summary>
public class AdminBadgeListViewModel
{
    public IEnumerable<AdminBadgeViewModel> Badges { get; set; } = new List<AdminBadgeViewModel>();
    public BadgeCategory? FilterCategory { get; set; }
    public bool ShowInactive { get; set; }
}

/// <summary>
/// View model for admin badge display with stats.
/// </summary>
public class AdminBadgeViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public BadgeCategory Category { get; set; }
    public BadgeRequirementType RequirementType { get; set; }
    public int RequirementValue { get; set; }
    public string? RequirementContext { get; set; }
    public int BonusPoints { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }

    // Stats
    public int TimesEarned { get; set; }
    public DateTime? LastEarnedAt { get; set; }
}

/// <summary>
/// View model for creating or editing a badge.
/// </summary>
public class BadgeEditViewModel
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public BadgeCategory Category { get; set; }
    public BadgeRequirementType RequirementType { get; set; }
    public int RequirementValue { get; set; }
    public string? RequirementContext { get; set; }
    public int BonusPoints { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public bool IsNew => !Id.HasValue;
}

/// <summary>
/// View model for manually awarding a badge.
/// </summary>
public class AwardBadgeViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int BadgeId { get; set; }
    public string? Context { get; set; }

    public IEnumerable<Badge> AvailableBadges { get; set; } = new List<Badge>();
}

/// <summary>
/// View model for adjusting student points.
/// </summary>
public class AdjustPointsViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int CurrentPoints { get; set; }
    public int Adjustment { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// View model for student gamification summary (admin view).
/// </summary>
public class StudentGamificationSummaryViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public int TotalPoints { get; set; }
    public int TokensEarnedFromPoints { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int BadgesEarned { get; set; }
    public bool HasActiveSubscription { get; set; }
    public decimal PointsMultiplier { get; set; }

    public IEnumerable<StudentBadge> RecentBadges { get; set; } = new List<StudentBadge>();
    public IEnumerable<PointTransaction> RecentPoints { get; set; } = new List<PointTransaction>();
}
