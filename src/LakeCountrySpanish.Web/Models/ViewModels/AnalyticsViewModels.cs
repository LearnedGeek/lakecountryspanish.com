using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// High-level metrics for the admin dashboard.
/// </summary>
public class AdminDashboardMetrics
{
    /// <summary>
    /// Monthly Recurring Revenue from active subscriptions.
    /// </summary>
    public decimal MRR { get; set; }

    /// <summary>
    /// Revenue change percentage compared to previous period.
    /// </summary>
    public decimal RevenueChangePercent { get; set; }

    /// <summary>
    /// Total active students (students with activity in last 30 days).
    /// </summary>
    public int ActiveStudents { get; set; }

    /// <summary>
    /// Total registered students.
    /// </summary>
    public int TotalStudents { get; set; }

    /// <summary>
    /// Active subscriptions count.
    /// </summary>
    public int ActiveSubscriptions { get; set; }

    /// <summary>
    /// Monthly churn rate percentage.
    /// </summary>
    public decimal ChurnRate { get; set; }

    /// <summary>
    /// Classes completed this month.
    /// </summary>
    public int ClassesThisMonth { get; set; }

    /// <summary>
    /// Assignments completed this month.
    /// </summary>
    public int AssignmentsCompletedThisMonth { get; set; }

    /// <summary>
    /// Total points earned by all students this month.
    /// </summary>
    public int PointsEarnedThisMonth { get; set; }
}

/// <summary>
/// Revenue breakdown by payment type.
/// </summary>
public class RevenueMetrics
{
    public decimal TotalRevenue { get; set; }
    public decimal SubscriptionRevenue { get; set; }
    public decimal TokenRevenue { get; set; }
    public decimal PackageRevenue { get; set; }
    public decimal CustomRateRevenue { get; set; }

    public decimal SubscriptionPercent => TotalRevenue > 0 ? Math.Round(SubscriptionRevenue / TotalRevenue * 100, 1) : 0;
    public decimal TokenPercent => TotalRevenue > 0 ? Math.Round(TokenRevenue / TotalRevenue * 100, 1) : 0;
    public decimal PackagePercent => TotalRevenue > 0 ? Math.Round(PackageRevenue / TotalRevenue * 100, 1) : 0;
    public decimal CustomRatePercent => TotalRevenue > 0 ? Math.Round(CustomRateRevenue / TotalRevenue * 100, 1) : 0;
}

/// <summary>
/// Subscription health metrics.
/// </summary>
public class SubscriptionMetrics
{
    public int ActiveCount { get; set; }
    public int PausedCount { get; set; }
    public int PastDueCount { get; set; }
    public int CancelledThisMonth { get; set; }
    public int NewThisMonth { get; set; }

    /// <summary>
    /// Monthly Recurring Revenue.
    /// </summary>
    public decimal MRR { get; set; }

    /// <summary>
    /// Churn rate = Cancelled / (Active at start of month) * 100
    /// </summary>
    public decimal ChurnRate { get; set; }

    /// <summary>
    /// Breakdown by subscription tier.
    /// </summary>
    public IEnumerable<SubscriptionTierBreakdown> TierBreakdown { get; set; } = new List<SubscriptionTierBreakdown>();
}

/// <summary>
/// Subscription counts and revenue by tier.
/// </summary>
public class SubscriptionTierBreakdown
{
    public string TierName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal TotalMRR => Count * MonthlyPrice;
}

/// <summary>
/// Student engagement metrics.
/// </summary>
public class EngagementMetrics
{
    public int ClassesCompleted { get; set; }
    public int AssignmentsCompleted { get; set; }
    public int TotalPointsEarned { get; set; }
    public int BadgesAwarded { get; set; }

    /// <summary>
    /// Percentage of students with activity in the period.
    /// </summary>
    public decimal ActiveStudentPercent { get; set; }

    /// <summary>
    /// Average points per active student.
    /// </summary>
    public decimal AveragePointsPerStudent { get; set; }

    /// <summary>
    /// Average assignment completion rate.
    /// </summary>
    public decimal AverageCompletionRate { get; set; }
}

/// <summary>
/// Daily revenue data point for charts.
/// </summary>
public class DailyRevenueData
{
    public DateTime Date { get; set; }
    public decimal SubscriptionRevenue { get; set; }
    public decimal TokenRevenue { get; set; }
    public decimal PackageRevenue { get; set; }
    public decimal CustomRateRevenue { get; set; }
    public decimal TotalRevenue => SubscriptionRevenue + TokenRevenue + PackageRevenue + CustomRateRevenue;
}

/// <summary>
/// Monthly revenue data point for charts.
/// </summary>
public class MonthlyRevenueData
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMM yyyy");
    public decimal SubscriptionRevenue { get; set; }
    public decimal TokenRevenue { get; set; }
    public decimal PackageRevenue { get; set; }
    public decimal CustomRateRevenue { get; set; }
    public decimal TotalRevenue => SubscriptionRevenue + TokenRevenue + PackageRevenue + CustomRateRevenue;
}

/// <summary>
/// Summary of student engagement for dashboard lists.
/// </summary>
public class StudentEngagementSummary
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int CurrentStreak { get; set; }
    public int AssignmentsCompleted { get; set; }
    public int ClassesAttended { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public int DaysSinceLastActivity => LastActivityDate.HasValue
        ? (int)(DateTime.UtcNow - LastActivityDate.Value).TotalDays
        : 999;
}

/// <summary>
/// Overall assignment difficulty feedback report.
/// </summary>
public class AssignmentDifficultyReport
{
    public int TotalFeedbackCount { get; set; }
    public int TooEasyCount { get; set; }
    public int JustRightCount { get; set; }
    public int TooHardCount { get; set; }

    public decimal TooEasyPercent => TotalFeedbackCount > 0 ? Math.Round((decimal)TooEasyCount / TotalFeedbackCount * 100, 1) : 0;
    public decimal JustRightPercent => TotalFeedbackCount > 0 ? Math.Round((decimal)JustRightCount / TotalFeedbackCount * 100, 1) : 0;
    public decimal TooHardPercent => TotalFeedbackCount > 0 ? Math.Round((decimal)TooHardCount / TotalFeedbackCount * 100, 1) : 0;

    /// <summary>
    /// Breakdown by CEFR level.
    /// </summary>
    public IEnumerable<DifficultyByLevel> ByLevel { get; set; } = new List<DifficultyByLevel>();

    /// <summary>
    /// Breakdown by assignment type.
    /// </summary>
    public IEnumerable<DifficultyByType> ByType { get; set; } = new List<DifficultyByType>();
}

/// <summary>
/// Difficulty feedback breakdown by CEFR level.
/// </summary>
public class DifficultyByLevel
{
    public string CefrLevel { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int TooEasyCount { get; set; }
    public int JustRightCount { get; set; }
    public int TooHardCount { get; set; }
    public decimal TooEasyPercent => TotalCount > 0 ? Math.Round((decimal)TooEasyCount / TotalCount * 100, 1) : 0;
    public decimal JustRightPercent => TotalCount > 0 ? Math.Round((decimal)JustRightCount / TotalCount * 100, 1) : 0;
    public decimal TooHardPercent => TotalCount > 0 ? Math.Round((decimal)TooHardCount / TotalCount * 100, 1) : 0;
}

/// <summary>
/// Difficulty feedback breakdown by assignment type.
/// </summary>
public class DifficultyByType
{
    public AssignmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public int TotalCount { get; set; }
    public int TooEasyCount { get; set; }
    public int JustRightCount { get; set; }
    public int TooHardCount { get; set; }
    public decimal TooEasyPercent => TotalCount > 0 ? Math.Round((decimal)TooEasyCount / TotalCount * 100, 1) : 0;
    public decimal JustRightPercent => TotalCount > 0 ? Math.Round((decimal)JustRightCount / TotalCount * 100, 1) : 0;
    public decimal TooHardPercent => TotalCount > 0 ? Math.Round((decimal)TooHardCount / TotalCount * 100, 1) : 0;
}

/// <summary>
/// Individual assignment with difficulty feedback data.
/// </summary>
public class AssignmentDifficultyItem
{
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = string.Empty;
    public AssignmentType Type { get; set; }
    public int SubmissionCount { get; set; }
    public decimal AverageScore { get; set; }
    public int TooEasyCount { get; set; }
    public int JustRightCount { get; set; }
    public int TooHardCount { get; set; }
    public string DominantFeedback { get; set; } = string.Empty;
    public bool NeedsAdjustment { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the difficulty analysis admin page.
/// </summary>
public class DifficultyAnalysisViewModel
{
    public AssignmentDifficultyReport Report { get; set; } = new();
    public IEnumerable<AssignmentDifficultyItem> TooEasyAssignments { get; set; } = new List<AssignmentDifficultyItem>();
    public IEnumerable<AssignmentDifficultyItem> TooHardAssignments { get; set; } = new List<AssignmentDifficultyItem>();
    public IEnumerable<AssignmentDifficultyItem> AllAssignments { get; set; } = new List<AssignmentDifficultyItem>();
}

/// <summary>
/// Weekly progress data for email reports.
/// </summary>
public class WeeklyProgressData
{
    public string StudentName { get; set; } = string.Empty;
    public int ClassesAttended { get; set; }
    public int AssignmentsCompleted { get; set; }
    public int PointsEarned { get; set; }
    public int CurrentStreak { get; set; }
    public int TotalPoints { get; set; }
    public IEnumerable<string> BadgesEarned { get; set; } = new List<string>();
    public string? NextMilestone { get; set; }
    public int PointsToNextMilestone { get; set; }
}
