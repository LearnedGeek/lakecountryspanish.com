using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service for managing points, badges, and streaks.
/// </summary>
public class GamificationService : IGamificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly ILogger<GamificationService> _logger;
    private const int PointsPerToken = 100;

    public GamificationService(
        ApplicationDbContext context,
        ITokenService tokenService,
        IEmailService emailService,
        ILogger<GamificationService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
    }

    #region Points Management

    public async Task<PointTransaction> AwardPointsAsync(
        string studentId,
        int basePoints,
        PointSource source,
        int? scheduledClassId = null,
        string? details = null)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
            throw new ArgumentException("Student not found", nameof(studentId));

        var multiplier = await GetPointsMultiplierAsync(studentId);
        var finalPoints = (int)Math.Round(basePoints * multiplier);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Update student's total points
            student.TotalPoints += finalPoints;
            var newBalance = student.TotalPoints;

            // Create transaction record
            var pointTxn = new PointTransaction
            {
                StudentId = studentId,
                Source = source,
                BasePoints = basePoints,
                Multiplier = multiplier,
                FinalPoints = finalPoints,
                BalanceAfter = newBalance,
                ScheduledClassId = scheduledClassId,
                Details = details,
                CreatedAt = DateTime.UtcNow
            };
            _context.PointTransactions.Add(pointTxn);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Awarded {FinalPoints} points ({BasePoints} x {Multiplier}) to student {StudentId} for {Source}",
                finalPoints, basePoints, multiplier, studentId, source);

            // Check for badge eligibility and token conversion (outside transaction)
            await CheckAndAwardBadgesAsync(studentId);
            await CheckAndConvertPointsToTokensAsync(studentId);

            return pointTxn;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<decimal> GetPointsMultiplierAsync(string studentId)
    {
        // Check if student has an active subscription
        var hasActiveSubscription = await _context.Subscriptions
            .AnyAsync(s => s.StudentId == studentId &&
                          s.Status == SubscriptionStatus.Active);

        if (hasActiveSubscription)
        {
            // Get the multiplier from their subscription tier
            var subscription = await _context.Subscriptions
                .Include(s => s.Tier)
                .FirstOrDefaultAsync(s => s.StudentId == studentId &&
                                         s.Status == SubscriptionStatus.Active);

            return subscription?.Tier?.PointsMultiplier ?? 1.5m;
        }

        return 1.0m;
    }

    public async Task<int> GetTotalPointsAsync(string studentId)
    {
        var student = await _context.Users.FindAsync(studentId);
        return student?.TotalPoints ?? 0;
    }

    public async Task<IEnumerable<PointTransaction>> GetPointHistoryAsync(string studentId, int? limit = null)
    {
        var query = _context.PointTransactions
            .Include(p => p.Badge)
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .AsQueryable();

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync();
    }

    public async Task<PointTransaction> AdjustPointsAsync(string studentId, int adjustment, string reason, string adminId)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
            throw new ArgumentException("Student not found", nameof(studentId));

        student.TotalPoints = Math.Max(0, student.TotalPoints + adjustment);

        var pointTxn = new PointTransaction
        {
            StudentId = studentId,
            Source = PointSource.Adjustment,
            BasePoints = adjustment,
            Multiplier = 1.0m,
            FinalPoints = adjustment,
            BalanceAfter = student.TotalPoints,
            Details = $"Admin adjustment by {adminId}: {reason}",
            CreatedAt = DateTime.UtcNow
        };
        _context.PointTransactions.Add(pointTxn);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} adjusted points by {Adjustment} for student {StudentId}: {Reason}",
            adminId, adjustment, studentId, reason);

        return pointTxn;
    }

    #endregion

    #region Streak Management

    public async Task<StudentStreak> GetStreakAsync(string studentId)
    {
        var streak = await _context.StudentStreaks
            .FirstOrDefaultAsync(s => s.StudentId == studentId);

        if (streak == null)
        {
            streak = new StudentStreak
            {
                StudentId = studentId,
                CurrentStreak = 0,
                LongestStreak = 0,
                TotalActiveDays = 0
            };
            _context.StudentStreaks.Add(streak);
            await _context.SaveChangesAsync();
        }

        return streak;
    }

    public async Task<StudentStreak> RecordActivityAsync(string studentId)
    {
        var streak = await GetStreakAsync(studentId);
        var today = DateTime.UtcNow.Date;

        // Already active today - nothing to do
        if (streak.LastActivityDate?.Date == today)
            return streak;

        var yesterday = today.AddDays(-1);

        // Check if continuing streak or starting new one
        if (streak.LastActivityDate?.Date == yesterday)
        {
            // Continuing streak
            streak.CurrentStreak++;
        }
        else
        {
            // Starting new streak (or first activity ever)
            streak.CurrentStreak = 1;
        }

        streak.LastActivityDate = DateTime.UtcNow;
        streak.TotalActiveDays++;
        streak.UpdatedAt = DateTime.UtcNow;

        // Update longest streak if needed
        if (streak.CurrentStreak > streak.LongestStreak)
            streak.LongestStreak = streak.CurrentStreak;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Recorded activity for student {StudentId}, streak now {Streak} days",
            studentId, streak.CurrentStreak);

        // Award streak bonus points for milestones
        await AwardStreakBonusAsync(studentId, streak.CurrentStreak);

        // Check for streak-related badges
        await CheckAndAwardBadgesAsync(studentId);

        return streak;
    }

    private async Task AwardStreakBonusAsync(string studentId, int streakDays)
    {
        // Award bonus points at streak milestones
        var bonusPoints = streakDays switch
        {
            7 => 25,   // 1 week streak
            14 => 50,  // 2 week streak
            30 => 100, // 1 month streak
            60 => 150, // 2 month streak
            90 => 200, // 3 month streak
            _ => 0
        };

        if (bonusPoints > 0)
        {
            await AwardPointsAsync(
                studentId,
                bonusPoints,
                PointSource.StreakBonus,
                details: $"Streak bonus for {streakDays} day streak!");
        }
    }

    public async Task<int> ResetExpiredStreaksAsync()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);

        var expiredStreaks = await _context.StudentStreaks
            .Where(s => s.CurrentStreak > 0 &&
                       s.LastActivityDate != null &&
                       s.LastActivityDate.Value.Date < yesterday)
            .ToListAsync();

        foreach (var streak in expiredStreaks)
        {
            streak.CurrentStreak = 0;
            streak.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        if (expiredStreaks.Any())
        {
            _logger.LogInformation("Reset {Count} expired streaks", expiredStreaks.Count);
        }

        return expiredStreaks.Count;
    }

    #endregion

    #region Badge Management

    public async Task<IEnumerable<Badge>> GetAllBadgesAsync()
    {
        return await _context.Badges
            .Where(b => b.IsActive)
            .OrderBy(b => b.Category)
            .ThenBy(b => b.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<Badge>> GetBadgesByCategoryAsync(BadgeCategory category)
    {
        return await _context.Badges
            .Where(b => b.IsActive && b.Category == category)
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<StudentBadge>> GetStudentBadgesAsync(string studentId)
    {
        return await _context.StudentBadges
            .Include(sb => sb.Badge)
            .Where(sb => sb.StudentId == studentId)
            .OrderByDescending(sb => sb.EarnedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StudentBadge>> GetUnviewedBadgesAsync(string studentId)
    {
        return await _context.StudentBadges
            .Include(sb => sb.Badge)
            .Where(sb => sb.StudentId == studentId && !sb.IsViewed)
            .OrderByDescending(sb => sb.EarnedAt)
            .ToListAsync();
    }

    public async Task MarkBadgesAsViewedAsync(string studentId)
    {
        var unviewedBadges = await _context.StudentBadges
            .Where(sb => sb.StudentId == studentId && !sb.IsViewed)
            .ToListAsync();

        foreach (var badge in unviewedBadges)
        {
            badge.IsViewed = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasBadgeAsync(string studentId, int badgeId)
    {
        return await _context.StudentBadges
            .AnyAsync(sb => sb.StudentId == studentId && sb.BadgeId == badgeId);
    }

    public async Task<IEnumerable<StudentBadge>> CheckAndAwardBadgesAsync(string studentId)
    {
        var awardedBadges = new List<StudentBadge>();
        var student = await _context.Users.FindAsync(studentId);
        if (student == null) return awardedBadges;

        var streak = await GetStreakAsync(studentId);
        var allBadges = await GetAllBadgesAsync();
        var earnedBadgeIds = (await GetStudentBadgesAsync(studentId))
            .Select(sb => sb.BadgeId)
            .ToHashSet();

        foreach (var badge in allBadges)
        {
            if (earnedBadgeIds.Contains(badge.Id))
                continue;

            var eligible = await CheckBadgeEligibilityAsync(studentId, badge, student, streak);
            if (eligible)
            {
                var studentBadge = await AwardBadgeAsync(studentId, badge.Id,
                    GetBadgeEarnedContext(badge, student, streak));

                if (studentBadge != null)
                    awardedBadges.Add(studentBadge);
            }
        }

        return awardedBadges;
    }

    private async Task<bool> CheckBadgeEligibilityAsync(
        string studentId,
        Badge badge,
        ApplicationUser student,
        StudentStreak streak)
    {
        return badge.RequirementType switch
        {
            BadgeRequirementType.Points => student.TotalPoints >= badge.RequirementValue,

            BadgeRequirementType.Streak => streak.CurrentStreak >= badge.RequirementValue ||
                                           streak.LongestStreak >= badge.RequirementValue,

            BadgeRequirementType.ClassesCompleted =>
                await _context.ScheduledClasses.CountAsync(
                    sc => sc.StudentId == studentId && sc.Status == ClassStatus.Completed
                ) >= badge.RequirementValue,

            BadgeRequirementType.CefrLevel =>
                CompareCefrLevel(student.CefrLevel ?? "A1", badge.RequirementContext ?? "A1") >= 0,

            _ => false // Custom badges require manual award
        };
    }

    private static int CompareCefrLevel(string current, string required)
    {
        var levels = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        var currentIndex = Array.IndexOf(levels, current.ToUpper());
        var requiredIndex = Array.IndexOf(levels, required.ToUpper());
        return currentIndex - requiredIndex;
    }

    private static string GetBadgeEarnedContext(Badge badge, ApplicationUser student, StudentStreak streak)
    {
        return badge.RequirementType switch
        {
            BadgeRequirementType.Points => $"Reached {student.TotalPoints} points",
            BadgeRequirementType.Streak => $"Achieved {streak.LongestStreak} day streak",
            BadgeRequirementType.CefrLevel => $"Reached {student.CefrLevel} level",
            _ => "Badge earned"
        };
    }

    public async Task<StudentBadge?> AwardBadgeAsync(string studentId, int badgeId, string? context = null)
    {
        // Check if already has badge
        if (await HasBadgeAsync(studentId, badgeId))
            return null;

        var badge = await _context.Badges.FindAsync(badgeId);
        if (badge == null || !badge.IsActive)
            return null;

        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
            return null;

        var studentBadge = new StudentBadge
        {
            StudentId = studentId,
            BadgeId = badgeId,
            EarnedAt = DateTime.UtcNow,
            EarnedContext = context,
            IsViewed = false
        };

        _context.StudentBadges.Add(studentBadge);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Awarded badge {BadgeId} ({BadgeName}) to student {StudentId}",
            badgeId, badge.Name, studentId);

        // Award bonus points if badge has them
        if (badge.BonusPoints > 0)
        {
            await AwardPointsAsync(
                studentId,
                badge.BonusPoints,
                PointSource.BadgeEarned,
                details: $"Bonus for earning '{badge.Name}' badge");
        }

        // Send email notification for badge earned
        try
        {
            await _emailService.SendBadgeEarnedAsync(
                student.Email!,
                student.FirstName ?? student.Email!,
                badge.Name,
                badge.Description ?? "Keep up the great work!",
                badge.BonusPoints);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send badge earned email to {StudentId}", studentId);
        }

        return studentBadge;
    }

    #endregion

    #region Token Conversion

    public async Task<int> CheckAndConvertPointsToTokensAsync(string studentId)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student == null) return 0;

        var tokensToAward = student.TotalPoints / PointsPerToken;
        var currentTokensEarned = await _context.Tokens
            .Where(t => t.StudentId == studentId && t.Source == TokenSource.Earned)
            .SumAsync(t => t.Quantity);

        var newTokensNeeded = tokensToAward - currentTokensEarned;

        if (newTokensNeeded > 0)
        {
            await _tokenService.AwardEarnedTokensAsync(
                studentId,
                newTokensNeeded,
                $"Earned from reaching {tokensToAward * PointsPerToken} points");

            _logger.LogInformation(
                "Converted points to {Tokens} token(s) for student {StudentId}",
                newTokensNeeded, studentId);

            // Send milestone email notification
            var milestone = tokensToAward * PointsPerToken;
            try
            {
                await _emailService.SendPointMilestoneAsync(
                    student.Email!,
                    student.FirstName ?? student.Email!,
                    student.TotalPoints,
                    milestone,
                    newTokensNeeded);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send point milestone email to {StudentId}", studentId);
            }
        }

        return newTokensNeeded;
    }

    #endregion

    #region Progress Summary

    public async Task<GamificationProgress> GetProgressAsync(string studentId)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
            throw new ArgumentException("Student not found", nameof(studentId));

        var streak = await GetStreakAsync(studentId);
        var badges = await GetStudentBadgesAsync(studentId);
        var unviewedCount = await _context.StudentBadges
            .CountAsync(sb => sb.StudentId == studentId && !sb.IsViewed);

        var multiplier = await GetPointsMultiplierAsync(studentId);

        // Find badges close to earning
        var earnedBadgeIds = badges.Select(b => b.BadgeId).ToHashSet();
        var allBadges = await GetAllBadgesAsync();
        var nextEarnable = allBadges
            .Where(b => !earnedBadgeIds.Contains(b.Id))
            .Where(b => IsCloseToEarning(b, student, streak))
            .Take(3)
            .ToList();

        return new GamificationProgress
        {
            TotalPoints = student.TotalPoints,
            PointsTowardsNextToken = student.PointsTowardsNextToken,
            TokensEarnedFromPoints = student.TokensEarnedFromPoints,
            CurrentStreak = streak.IsStreakActive ? streak.CurrentStreak : 0,
            LongestStreak = streak.LongestStreak,
            WasActiveToday = streak.WasActiveToday,
            TotalBadgesEarned = badges.Count(),
            UnviewedBadgeCount = unviewedCount,
            PointsMultiplier = multiplier,
            RecentBadges = badges.Take(5),
            NextEarnableBadges = nextEarnable
        };
    }

    private static bool IsCloseToEarning(Badge badge, ApplicationUser student, StudentStreak streak)
    {
        return badge.RequirementType switch
        {
            BadgeRequirementType.Points =>
                student.TotalPoints >= badge.RequirementValue * 0.5,
            BadgeRequirementType.Streak =>
                streak.CurrentStreak >= badge.RequirementValue * 0.5 ||
                streak.LongestStreak >= badge.RequirementValue * 0.7,
            _ => false
        };
    }

    #endregion

    #region Admin Badge Management

    public async Task<Badge> CreateBadgeAsync(Badge badge)
    {
        badge.CreatedAt = DateTime.UtcNow;
        _context.Badges.Add(badge);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created badge {BadgeId}: {BadgeName}", badge.Id, badge.Name);
        return badge;
    }

    public async Task<Badge?> UpdateBadgeAsync(Badge badge)
    {
        var existing = await _context.Badges.FindAsync(badge.Id);
        if (existing == null) return null;

        existing.Name = badge.Name;
        existing.Description = badge.Description;
        existing.IconUrl = badge.IconUrl;
        existing.Emoji = badge.Emoji;
        existing.Category = badge.Category;
        existing.RequirementType = badge.RequirementType;
        existing.RequirementValue = badge.RequirementValue;
        existing.RequirementContext = badge.RequirementContext;
        existing.BonusPoints = badge.BonusPoints;
        existing.IsActive = badge.IsActive;
        existing.DisplayOrder = badge.DisplayOrder;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated badge {BadgeId}: {BadgeName}", badge.Id, badge.Name);
        return existing;
    }

    public async Task<bool> DisableBadgeAsync(int badgeId)
    {
        var badge = await _context.Badges.FindAsync(badgeId);
        if (badge == null) return false;

        badge.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Disabled badge {BadgeId}: {BadgeName}", badgeId, badge.Name);
        return true;
    }

    #endregion
}
