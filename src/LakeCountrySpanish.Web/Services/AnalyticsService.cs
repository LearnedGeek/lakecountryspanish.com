using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service for analytics and reporting functionality.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _context;

    public AnalyticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardMetrics> GetDashboardMetricsAsync()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfLastMonth = startOfMonth.AddMonths(-1);
        var thirtyDaysAgo = now.AddDays(-30);

        // MRR from active subscriptions
        var mrr = await _context.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .Include(s => s.Tier)
            .SumAsync(s => s.Tier.MonthlyPrice);

        // Revenue this month vs last month
        var revenueThisMonth = await _context.Payments
            .Where(p => p.Status == PaymentStatusType.Completed && p.CompletedAt >= startOfMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var revenueLastMonth = await _context.Payments
            .Where(p => p.Status == PaymentStatusType.Completed &&
                       p.CompletedAt >= startOfLastMonth && p.CompletedAt < startOfMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var revenueChangePercent = revenueLastMonth > 0
            ? Math.Round((revenueThisMonth - revenueLastMonth) / revenueLastMonth * 100, 1)
            : 0;

        // Student counts
        var totalStudents = await _context.Users
            .Where(u => u.ScheduledClasses.Any() || u.StudentPackages.Any() || u.Subscriptions.Any())
            .CountAsync();

        // Active students = those with point transactions in last 30 days
        var activeStudents = await _context.PointTransactions
            .Where(p => p.CreatedAt >= thirtyDaysAgo)
            .Select(p => p.StudentId)
            .Distinct()
            .CountAsync();

        // Active subscriptions
        var activeSubscriptions = await _context.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .CountAsync();

        // Churn rate: cancelled this month / active at start of month * 100
        var cancelledThisMonth = await _context.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Cancelled && s.CancelledAt >= startOfMonth)
            .CountAsync();

        var activeAtStartOfMonth = await _context.Subscriptions
            .Where(s => (s.Status == SubscriptionStatus.Active && s.StartDate < startOfMonth) ||
                       (s.Status == SubscriptionStatus.Cancelled && s.CancelledAt >= startOfMonth && s.StartDate < startOfMonth))
            .CountAsync();

        var churnRate = activeAtStartOfMonth > 0
            ? Math.Round((decimal)cancelledThisMonth / activeAtStartOfMonth * 100, 1)
            : 0;

        // Classes completed this month
        var classesThisMonth = await _context.ScheduledClasses
            .Where(c => c.Status == ClassStatus.Completed && c.ClassDateTime >= startOfMonth)
            .CountAsync();

        // Assignments completed this month
        var assignmentsThisMonth = await _context.AssignmentSubmissions
            .Where(s => s.SubmittedAt >= startOfMonth)
            .CountAsync();

        // Points earned this month
        var pointsThisMonth = await _context.PointTransactions
            .Where(p => p.CreatedAt >= startOfMonth && p.FinalPoints > 0)
            .SumAsync(p => (int?)p.FinalPoints) ?? 0;

        return new AdminDashboardMetrics
        {
            MRR = mrr,
            RevenueChangePercent = revenueChangePercent,
            TotalStudents = totalStudents,
            ActiveStudents = activeStudents,
            ActiveSubscriptions = activeSubscriptions,
            ChurnRate = churnRate,
            ClassesThisMonth = classesThisMonth,
            AssignmentsCompletedThisMonth = assignmentsThisMonth,
            PointsEarnedThisMonth = pointsThisMonth
        };
    }

    public async Task<RevenueMetrics> GetRevenueMetricsAsync(DateTime startDate, DateTime endDate)
    {
        var payments = await _context.Payments
            .Where(p => p.Status == PaymentStatusType.Completed &&
                       p.CompletedAt >= startDate && p.CompletedAt < endDate)
            .GroupBy(p => p.PaymentType)
            .Select(g => new { PaymentType = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var subscriptionRevenue = payments.FirstOrDefault(p => p.PaymentType == PaymentType.Subscription)?.Total ?? 0;
        var tokenRevenue = payments.FirstOrDefault(p => p.PaymentType == PaymentType.TokenPurchase)?.Total ?? 0;
        var packageRevenue = payments.FirstOrDefault(p => p.PaymentType == PaymentType.Package)?.Total ?? 0;
        var customRateRevenue = payments.FirstOrDefault(p => p.PaymentType == PaymentType.SingleClass)?.Total ?? 0;

        return new RevenueMetrics
        {
            TotalRevenue = subscriptionRevenue + tokenRevenue + packageRevenue + customRateRevenue,
            SubscriptionRevenue = subscriptionRevenue,
            TokenRevenue = tokenRevenue,
            PackageRevenue = packageRevenue,
            CustomRateRevenue = customRateRevenue
        };
    }

    public async Task<SubscriptionMetrics> GetSubscriptionMetricsAsync()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var subscriptions = await _context.Subscriptions
            .Include(s => s.Tier)
            .ToListAsync();

        var activeCount = subscriptions.Count(s => s.Status == SubscriptionStatus.Active);
        var pausedCount = subscriptions.Count(s => s.Status == SubscriptionStatus.Paused);
        var pastDueCount = subscriptions.Count(s => s.Status == SubscriptionStatus.PastDue);
        var cancelledThisMonth = subscriptions.Count(s => s.Status == SubscriptionStatus.Cancelled && s.CancelledAt >= startOfMonth);
        var newThisMonth = subscriptions.Count(s => s.StartDate >= startOfMonth);

        var mrr = subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .Sum(s => s.Tier?.MonthlyPrice ?? 0);

        // Churn calculation
        var activeAtStartOfMonth = subscriptions.Count(s =>
            (s.Status == SubscriptionStatus.Active && s.StartDate < startOfMonth) ||
            (s.Status == SubscriptionStatus.Cancelled && s.CancelledAt >= startOfMonth && s.StartDate < startOfMonth));

        var churnRate = activeAtStartOfMonth > 0
            ? Math.Round((decimal)cancelledThisMonth / activeAtStartOfMonth * 100, 1)
            : 0;

        // Tier breakdown
        var tierBreakdown = subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .GroupBy(s => s.Tier?.Name ?? "Unknown")
            .Select(g => new SubscriptionTierBreakdown
            {
                TierName = g.Key,
                Count = g.Count(),
                MonthlyPrice = g.First().Tier?.MonthlyPrice ?? 0
            })
            .OrderBy(t => t.MonthlyPrice)
            .ToList();

        return new SubscriptionMetrics
        {
            ActiveCount = activeCount,
            PausedCount = pausedCount,
            PastDueCount = pastDueCount,
            CancelledThisMonth = cancelledThisMonth,
            NewThisMonth = newThisMonth,
            MRR = mrr,
            ChurnRate = churnRate,
            TierBreakdown = tierBreakdown
        };
    }

    public async Task<EngagementMetrics> GetEngagementMetricsAsync(DateTime startDate, DateTime endDate)
    {
        // Classes completed
        var classesCompleted = await _context.ScheduledClasses
            .Where(c => c.Status == ClassStatus.Completed &&
                       c.ClassDateTime >= startDate && c.ClassDateTime < endDate)
            .CountAsync();

        // Assignments completed
        var assignmentsCompleted = await _context.AssignmentSubmissions
            .Where(s => s.SubmittedAt >= startDate && s.SubmittedAt < endDate)
            .CountAsync();

        // Total points earned
        var totalPoints = await _context.PointTransactions
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt < endDate && p.FinalPoints > 0)
            .SumAsync(p => (int?)p.FinalPoints) ?? 0;

        // Badges awarded
        var badgesAwarded = await _context.StudentBadges
            .Where(b => b.EarnedAt >= startDate && b.EarnedAt < endDate)
            .CountAsync();

        // Active students (with point transactions in period)
        var activeStudentIds = await _context.PointTransactions
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt < endDate)
            .Select(p => p.StudentId)
            .Distinct()
            .ToListAsync();

        // Total students who could be active
        var totalStudents = await _context.Users
            .Where(u => u.ScheduledClasses.Any() || u.StudentPackages.Any() || u.Subscriptions.Any())
            .CountAsync();

        var activeStudentPercent = totalStudents > 0
            ? Math.Round((decimal)activeStudentIds.Count / totalStudents * 100, 1)
            : 0;

        var avgPointsPerStudent = activeStudentIds.Count > 0
            ? Math.Round((decimal)totalPoints / activeStudentIds.Count, 1)
            : 0;

        // Average assignment completion rate (for assigned assignments)
        var assignedCount = await _context.StudentAssignments
            .Where(sa => sa.AssignedAt >= startDate && sa.AssignedAt < endDate)
            .CountAsync();

        var completedCount = await _context.StudentAssignments
            .Where(sa => sa.AssignedAt >= startDate && sa.AssignedAt < endDate &&
                        sa.Status == StudentAssignmentStatus.Completed)
            .CountAsync();

        var avgCompletionRate = assignedCount > 0
            ? Math.Round((decimal)completedCount / assignedCount * 100, 1)
            : 0;

        return new EngagementMetrics
        {
            ClassesCompleted = classesCompleted,
            AssignmentsCompleted = assignmentsCompleted,
            TotalPointsEarned = totalPoints,
            BadgesAwarded = badgesAwarded,
            ActiveStudentPercent = activeStudentPercent,
            AveragePointsPerStudent = avgPointsPerStudent,
            AverageCompletionRate = avgCompletionRate
        };
    }

    public async Task<IEnumerable<DailyRevenueData>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate)
    {
        var payments = await _context.Payments
            .Where(p => p.Status == PaymentStatusType.Completed &&
                       p.CompletedAt >= startDate && p.CompletedAt < endDate)
            .ToListAsync();

        var dailyData = payments
            .GroupBy(p => p.CompletedAt?.Date ?? p.CreatedAt.Date)
            .Select(g => new DailyRevenueData
            {
                Date = g.Key,
                SubscriptionRevenue = g.Where(p => p.PaymentType == PaymentType.Subscription).Sum(p => p.Amount),
                TokenRevenue = g.Where(p => p.PaymentType == PaymentType.TokenPurchase).Sum(p => p.Amount),
                PackageRevenue = g.Where(p => p.PaymentType == PaymentType.Package).Sum(p => p.Amount),
                CustomRateRevenue = g.Where(p => p.PaymentType == PaymentType.SingleClass).Sum(p => p.Amount)
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Fill in missing days with zeros
        var result = new List<DailyRevenueData>();
        for (var date = startDate.Date; date < endDate.Date; date = date.AddDays(1))
        {
            var existing = dailyData.FirstOrDefault(d => d.Date == date);
            result.Add(existing ?? new DailyRevenueData { Date = date });
        }

        return result;
    }

    public async Task<IEnumerable<MonthlyRevenueData>> GetMonthlyRevenueAsync(int months = 12)
    {
        var now = DateTime.UtcNow;
        var startDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-months + 1);

        var payments = await _context.Payments
            .Where(p => p.Status == PaymentStatusType.Completed && p.CompletedAt >= startDate)
            .ToListAsync();

        var monthlyData = payments
            .GroupBy(p => new { Year = (p.CompletedAt ?? p.CreatedAt).Year, Month = (p.CompletedAt ?? p.CreatedAt).Month })
            .Select(g => new MonthlyRevenueData
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                SubscriptionRevenue = g.Where(p => p.PaymentType == PaymentType.Subscription).Sum(p => p.Amount),
                TokenRevenue = g.Where(p => p.PaymentType == PaymentType.TokenPurchase).Sum(p => p.Amount),
                PackageRevenue = g.Where(p => p.PaymentType == PaymentType.Package).Sum(p => p.Amount),
                CustomRateRevenue = g.Where(p => p.PaymentType == PaymentType.SingleClass).Sum(p => p.Amount)
            })
            .OrderBy(d => d.Year).ThenBy(d => d.Month)
            .ToList();

        // Fill in missing months with zeros
        var result = new List<MonthlyRevenueData>();
        for (var date = startDate; date <= now; date = date.AddMonths(1))
        {
            var existing = monthlyData.FirstOrDefault(d => d.Year == date.Year && d.Month == date.Month);
            result.Add(existing ?? new MonthlyRevenueData { Year = date.Year, Month = date.Month });
        }

        return result;
    }

    public async Task<IEnumerable<StudentEngagementSummary>> GetTopEngagedStudentsAsync(int count = 10)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        // Get total points per student
        var studentPoints = await _context.PointTransactions
            .Where(p => p.FinalPoints > 0)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, TotalPoints = g.Sum(p => p.FinalPoints) })
            .OrderByDescending(s => s.TotalPoints)
            .Take(count * 2) // Get extra to filter out inactive
            .ToListAsync();

        var studentIds = studentPoints.Select(s => s.StudentId).ToList();

        // Get student details
        var students = await _context.Users
            .Where(u => studentIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email
            })
            .ToListAsync();

        // Get streaks
        var streaks = await _context.StudentStreaks
            .Where(s => studentIds.Contains(s.StudentId))
            .ToDictionaryAsync(s => s.StudentId, s => s.CurrentStreak);

        // Get last activity dates
        var lastActivity = await _context.PointTransactions
            .Where(p => studentIds.Contains(p.StudentId))
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, LastActivity = g.Max(p => p.CreatedAt) })
            .ToDictionaryAsync(x => x.StudentId, x => x.LastActivity);

        // Get assignment counts
        var assignmentCounts = await _context.AssignmentSubmissions
            .Where(s => studentIds.Contains(s.StudentId))
            .GroupBy(s => s.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);

        // Get class counts
        var classCounts = await _context.ScheduledClasses
            .Where(c => studentIds.Contains(c.StudentId) && c.Status == ClassStatus.Completed)
            .GroupBy(c => c.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);

        var result = studentPoints
            .Select(sp =>
            {
                var student = students.FirstOrDefault(s => s.Id == sp.StudentId);
                return new StudentEngagementSummary
                {
                    StudentId = sp.StudentId,
                    StudentName = student != null ? $"{student.FirstName} {student.LastName}" : "Unknown",
                    Email = student?.Email ?? "",
                    TotalPoints = sp.TotalPoints,
                    CurrentStreak = streaks.GetValueOrDefault(sp.StudentId, 0),
                    AssignmentsCompleted = assignmentCounts.GetValueOrDefault(sp.StudentId, 0),
                    ClassesAttended = classCounts.GetValueOrDefault(sp.StudentId, 0),
                    LastActivityDate = lastActivity.GetValueOrDefault(sp.StudentId)
                };
            })
            .Take(count)
            .ToList();

        return result;
    }

    public async Task<IEnumerable<StudentEngagementSummary>> GetAtRiskStudentsAsync(int count = 10)
    {
        var fourteenDaysAgo = DateTime.UtcNow.AddDays(-14);

        // Get students who haven't had any point transactions in 14+ days
        // but have had activity before (they're not brand new)
        var activeStudentIds = await _context.PointTransactions
            .Where(p => p.CreatedAt >= fourteenDaysAgo)
            .Select(p => p.StudentId)
            .Distinct()
            .ToListAsync();

        // Students with past activity but not recently
        var atRiskStudents = await _context.PointTransactions
            .Where(p => !activeStudentIds.Contains(p.StudentId))
            .GroupBy(p => p.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                TotalPoints = g.Sum(p => p.FinalPoints),
                LastActivity = g.Max(p => p.CreatedAt)
            })
            .OrderByDescending(s => s.LastActivity)
            .Take(count * 2)
            .ToListAsync();

        var studentIds = atRiskStudents.Select(s => s.StudentId).ToList();

        // Get student details
        var students = await _context.Users
            .Where(u => studentIds.Contains(u.Id) && u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email
            })
            .ToListAsync();

        // Get streaks
        var streaks = await _context.StudentStreaks
            .Where(s => studentIds.Contains(s.StudentId))
            .ToDictionaryAsync(s => s.StudentId, s => s.CurrentStreak);

        // Get assignment counts
        var assignmentCounts = await _context.AssignmentSubmissions
            .Where(s => studentIds.Contains(s.StudentId))
            .GroupBy(s => s.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);

        // Get class counts
        var classCounts = await _context.ScheduledClasses
            .Where(c => studentIds.Contains(c.StudentId) && c.Status == ClassStatus.Completed)
            .GroupBy(c => c.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);

        var result = atRiskStudents
            .Where(ar => students.Any(s => s.Id == ar.StudentId))
            .Select(ar =>
            {
                var student = students.FirstOrDefault(s => s.Id == ar.StudentId);
                return new StudentEngagementSummary
                {
                    StudentId = ar.StudentId,
                    StudentName = student != null ? $"{student.FirstName} {student.LastName}" : "Unknown",
                    Email = student?.Email ?? "",
                    TotalPoints = ar.TotalPoints,
                    CurrentStreak = streaks.GetValueOrDefault(ar.StudentId, 0),
                    AssignmentsCompleted = assignmentCounts.GetValueOrDefault(ar.StudentId, 0),
                    ClassesAttended = classCounts.GetValueOrDefault(ar.StudentId, 0),
                    LastActivityDate = ar.LastActivity
                };
            })
            .Take(count)
            .ToList();

        return result;
    }

    public async Task<AssignmentDifficultyReport> GetDifficultyReportAsync()
    {
        var submissions = await _context.AssignmentSubmissions
            .Where(s => s.DifficultyFeedback != null)
            .Include(s => s.Assignment)
            .ToListAsync();

        var totalCount = submissions.Count;
        var tooEasyCount = submissions.Count(s => s.DifficultyFeedback == DifficultyFeedback.TooEasy);
        var justRightCount = submissions.Count(s => s.DifficultyFeedback == DifficultyFeedback.JustRight ||
                                                     s.DifficultyFeedback == DifficultyFeedback.Challenging);
        var tooHardCount = submissions.Count(s => s.DifficultyFeedback == DifficultyFeedback.TooHard);

        // Breakdown by CEFR level
        var byLevel = submissions
            .GroupBy(s => s.Assignment.CefrLevel)
            .Select(g => new DifficultyByLevel
            {
                CefrLevel = g.Key,
                TotalCount = g.Count(),
                TooEasyCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.TooEasy),
                JustRightCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.JustRight ||
                                              s.DifficultyFeedback == DifficultyFeedback.Challenging),
                TooHardCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.TooHard)
            })
            .OrderBy(d => d.CefrLevel)
            .ToList();

        // Breakdown by assignment type
        var byType = submissions
            .GroupBy(s => s.Assignment.Type)
            .Select(g => new DifficultyByType
            {
                Type = g.Key,
                TotalCount = g.Count(),
                TooEasyCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.TooEasy),
                JustRightCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.JustRight ||
                                              s.DifficultyFeedback == DifficultyFeedback.Challenging),
                TooHardCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.TooHard)
            })
            .OrderBy(d => d.Type)
            .ToList();

        return new AssignmentDifficultyReport
        {
            TotalFeedbackCount = totalCount,
            TooEasyCount = tooEasyCount,
            JustRightCount = justRightCount,
            TooHardCount = tooHardCount,
            ByLevel = byLevel,
            ByType = byType
        };
    }

    public async Task<IEnumerable<AssignmentDifficultyItem>> GetAssignmentsByDifficultyFeedbackAsync()
    {
        var assignmentsWithFeedback = await _context.AssignmentSubmissions
            .Where(s => s.DifficultyFeedback != null)
            .GroupBy(s => s.AssignmentId)
            .Select(g => new
            {
                AssignmentId = g.Key,
                SubmissionCount = g.Count(),
                AverageScore = g.Average(s => s.PercentageScore),
                TooEasyCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.TooEasy),
                JustRightCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.JustRight ||
                                              s.DifficultyFeedback == DifficultyFeedback.Challenging),
                TooHardCount = g.Count(s => s.DifficultyFeedback == DifficultyFeedback.TooHard)
            })
            .Where(a => a.SubmissionCount >= 3) // Only include assignments with enough feedback
            .ToListAsync();

        var assignmentIds = assignmentsWithFeedback.Select(a => a.AssignmentId).ToList();

        var assignments = await _context.Assignments
            .Where(a => assignmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var result = assignmentsWithFeedback
            .Select(af =>
            {
                var assignment = assignments.GetValueOrDefault(af.AssignmentId);
                var dominantFeedback = DetermineDominantFeedback(af.TooEasyCount, af.JustRightCount, af.TooHardCount);
                var needsAdjustment = ShouldAdjustDifficulty(af.AverageScore, dominantFeedback, af.SubmissionCount);

                return new AssignmentDifficultyItem
                {
                    AssignmentId = af.AssignmentId,
                    Title = assignment?.Title ?? "Unknown",
                    CefrLevel = assignment?.CefrLevel ?? "Unknown",
                    Type = assignment?.Type ?? AssignmentType.MultipleChoice,
                    SubmissionCount = af.SubmissionCount,
                    AverageScore = Math.Round(af.AverageScore, 1),
                    TooEasyCount = af.TooEasyCount,
                    JustRightCount = af.JustRightCount,
                    TooHardCount = af.TooHardCount,
                    DominantFeedback = dominantFeedback,
                    NeedsAdjustment = needsAdjustment,
                    RecommendedAction = GetRecommendedAction(af.AverageScore, dominantFeedback, needsAdjustment)
                };
            })
            .OrderByDescending(a => a.NeedsAdjustment)
            .ThenByDescending(a => a.SubmissionCount)
            .ToList();

        return result;
    }

    private static string DetermineDominantFeedback(int tooEasy, int justRight, int tooHard)
    {
        if (tooEasy >= justRight && tooEasy >= tooHard) return "Too Easy";
        if (tooHard >= justRight && tooHard >= tooEasy) return "Too Hard";
        return "Just Right";
    }

    private static bool ShouldAdjustDifficulty(decimal averageScore, string dominantFeedback, int submissionCount)
    {
        if (submissionCount < 3) return false;

        // Too easy: high score AND dominant feedback is "too easy"
        if (dominantFeedback == "Too Easy" && averageScore >= 85) return true;

        // Too hard: low score AND dominant feedback is "too hard"
        if (dominantFeedback == "Too Hard" && averageScore <= 50) return true;

        return false;
    }

    private static string GetRecommendedAction(decimal averageScore, string dominantFeedback, bool needsAdjustment)
    {
        if (!needsAdjustment) return "No action needed";

        if (dominantFeedback == "Too Easy")
        {
            return averageScore >= 90
                ? "Consider increasing difficulty or moving to higher CEFR level"
                : "Consider adding more challenging questions";
        }

        if (dominantFeedback == "Too Hard")
        {
            return averageScore <= 40
                ? "Consider reducing difficulty or moving to lower CEFR level"
                : "Consider adding hints or simplifying questions";
        }

        return "Review assignment content";
    }
}
