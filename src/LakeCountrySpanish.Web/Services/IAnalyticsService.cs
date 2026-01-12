using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service for analytics and reporting functionality.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Get high-level dashboard metrics (MRR, engagement, churn).
    /// </summary>
    Task<AdminDashboardMetrics> GetDashboardMetricsAsync();

    /// <summary>
    /// Get revenue breakdown by payment type for a date range.
    /// </summary>
    Task<RevenueMetrics> GetRevenueMetricsAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get subscription health metrics.
    /// </summary>
    Task<SubscriptionMetrics> GetSubscriptionMetricsAsync();

    /// <summary>
    /// Get student engagement metrics for a date range.
    /// </summary>
    Task<EngagementMetrics> GetEngagementMetricsAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get daily revenue data for charts.
    /// </summary>
    Task<IEnumerable<DailyRevenueData>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get monthly revenue data for trend charts.
    /// </summary>
    Task<IEnumerable<MonthlyRevenueData>> GetMonthlyRevenueAsync(int months = 12);

    /// <summary>
    /// Get top engaged students by points.
    /// </summary>
    Task<IEnumerable<StudentEngagementSummary>> GetTopEngagedStudentsAsync(int count = 10);

    /// <summary>
    /// Get students at risk (inactive for 14+ days).
    /// </summary>
    Task<IEnumerable<StudentEngagementSummary>> GetAtRiskStudentsAsync(int count = 10);

    /// <summary>
    /// Get assignment difficulty feedback report.
    /// </summary>
    Task<AssignmentDifficultyReport> GetDifficultyReportAsync();

    /// <summary>
    /// Get assignments flagged for difficulty adjustment.
    /// </summary>
    Task<IEnumerable<AssignmentDifficultyItem>> GetAssignmentsByDifficultyFeedbackAsync();
}
