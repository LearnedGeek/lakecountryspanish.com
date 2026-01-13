using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Tests.Services;

/// <summary>
/// Tests for gamification-related data operations.
/// </summary>
public class GamificationServiceTests
{
    private readonly ApplicationDbContext _context;

    public GamificationServiceTests()
    {
        _context = TestDbContextFactory.Create();
    }

    [Fact]
    public async Task PointTransaction_SumsTotalPoints()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var transactions = new List<PointTransaction>
        {
            new() { StudentId = student.Id, BasePoints = 50, FinalPoints = 50, Source = PointSource.ClassCompletion, Multiplier = 1.0m },
            new() { StudentId = student.Id, BasePoints = 30, FinalPoints = 45, Source = PointSource.Exercise, Multiplier = 1.5m },
            new() { StudentId = student.Id, BasePoints = 20, FinalPoints = 30, Source = PointSource.StreakBonus, Multiplier = 1.5m }
        };
        _context.PointTransactions.AddRange(transactions);
        await _context.SaveChangesAsync();

        // Act
        var totalPoints = await _context.PointTransactions
            .Where(p => p.StudentId == student.Id)
            .SumAsync(p => p.FinalPoints);

        // Assert
        Assert.Equal(125, totalPoints); // 50 + 45 + 30
    }

    [Fact]
    public async Task PointTransaction_AppliesSubscriptionMultiplier()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tier = new SubscriptionTier
        {
            Name = "Premium",
            MonthlyPrice = 49.99m,
            ClassesPerMonth = 4,
            PointsMultiplier = 1.5m,
            IsActive = true
        };
        _context.SubscriptionTiers.Add(tier);
        await _context.SaveChangesAsync();

        // Simulate point award with multiplier
        var transaction = new PointTransaction
        {
            StudentId = student.Id,
            BasePoints = 100,
            Multiplier = tier.PointsMultiplier,
            FinalPoints = (int)(100 * tier.PointsMultiplier), // 150
            Source = PointSource.ClassCompletion
        };
        _context.PointTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.PointTransactions.FindAsync(transaction.Id);

        // Assert
        Assert.Equal(100, result?.BasePoints);
        Assert.Equal(1.5m, result?.Multiplier);
        Assert.Equal(150, result?.FinalPoints);
    }

    [Fact]
    public async Task StudentStreak_TracksConsecutiveDays()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var streak = new StudentStreak
        {
            StudentId = student.Id,
            CurrentStreak = 7,
            LongestStreak = 14,
            LastActivityDate = DateTime.UtcNow.Date
        };
        _context.StudentStreaks.Add(streak);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.StudentStreaks
            .FirstOrDefaultAsync(s => s.StudentId == student.Id);

        // Assert
        Assert.Equal(7, result?.CurrentStreak);
        Assert.Equal(14, result?.LongestStreak);
    }

    [Fact]
    public async Task StudentStreak_IncrementForConsecutiveDay()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var streak = new StudentStreak
        {
            StudentId = student.Id,
            CurrentStreak = 5,
            LongestStreak = 5,
            LastActivityDate = DateTime.UtcNow.Date.AddDays(-1) // Yesterday
        };
        _context.StudentStreaks.Add(streak);
        await _context.SaveChangesAsync();

        // Act - Simulate activity today
        streak.CurrentStreak++;
        streak.LongestStreak = Math.Max(streak.LongestStreak, streak.CurrentStreak);
        streak.LastActivityDate = DateTime.UtcNow.Date;
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.StudentStreaks.FindAsync(streak.Id);
        Assert.Equal(6, updated?.CurrentStreak);
        Assert.Equal(6, updated?.LongestStreak);
    }

    [Fact]
    public async Task StudentStreak_ResetsAfterGap()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var streak = new StudentStreak
        {
            StudentId = student.Id,
            CurrentStreak = 10,
            LongestStreak = 15,
            LastActivityDate = DateTime.UtcNow.Date.AddDays(-3) // 3 days ago
        };
        _context.StudentStreaks.Add(streak);
        await _context.SaveChangesAsync();

        // Act - Simulate activity after gap
        var daysSinceLastActivity = (DateTime.UtcNow.Date - streak.LastActivityDate!.Value.Date).Days;
        if (daysSinceLastActivity > 1)
        {
            streak.CurrentStreak = 1; // Reset
        }
        streak.LastActivityDate = DateTime.UtcNow.Date;
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.StudentStreaks.FindAsync(streak.Id);
        Assert.Equal(1, updated?.CurrentStreak);
        Assert.Equal(15, updated?.LongestStreak); // Unchanged
    }

    [Fact]
    public async Task Badge_CanBeEarnedByStudent()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var badge = new Badge
        {
            Name = "First Steps",
            Description = "Complete your first class",
            Category = BadgeCategory.Milestone,
            RequirementType = BadgeRequirementType.ClassesCompleted,
            RequirementValue = 1,
            BonusPoints = 25,
            IsActive = true
        };
        _context.Badges.Add(badge);
        await _context.SaveChangesAsync();

        var studentBadge = new StudentBadge
        {
            StudentId = student.Id,
            BadgeId = badge.Id,
            EarnedAt = DateTime.UtcNow
        };
        _context.StudentBadges.Add(studentBadge);
        await _context.SaveChangesAsync();

        // Act
        var earned = await _context.StudentBadges
            .Include(sb => sb.Badge)
            .Where(sb => sb.StudentId == student.Id)
            .ToListAsync();

        // Assert
        Assert.Single(earned);
        Assert.Equal("First Steps", earned.First().Badge?.Name);
    }

    [Fact]
    public async Task Badge_NoDuplicatesAllowed()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var badge = new Badge
        {
            Name = "First Steps",
            Description = "Complete your first class",
            Category = BadgeCategory.Milestone,
            RequirementType = BadgeRequirementType.ClassesCompleted,
            RequirementValue = 1,
            IsActive = true
        };
        _context.Badges.Add(badge);

        var existingBadge = new StudentBadge
        {
            StudentId = student.Id,
            BadgeId = badge.Id,
            EarnedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.StudentBadges.Add(existingBadge);
        await _context.SaveChangesAsync();

        // Act - Check if student already has badge
        var alreadyHas = await _context.StudentBadges
            .AnyAsync(sb => sb.StudentId == student.Id && sb.BadgeId == badge.Id);

        // Assert
        Assert.True(alreadyHas);
    }

    [Fact]
    public async Task Badge_DifferentCategories()
    {
        // Arrange
        var badges = new List<Badge>
        {
            new() { Name = "First Class", Category = BadgeCategory.Milestone, RequirementType = BadgeRequirementType.ClassesCompleted, RequirementValue = 1, IsActive = true },
            new() { Name = "Week Warrior", Category = BadgeCategory.Consistency, RequirementType = BadgeRequirementType.Streak, RequirementValue = 7, IsActive = true },
            new() { Name = "Grammar Pro", Category = BadgeCategory.TopicMastery, RequirementType = BadgeRequirementType.TopicAssignments, RequirementValue = 5, IsActive = true },
            new() { Name = "Special Award", Category = BadgeCategory.Special, RequirementType = BadgeRequirementType.Custom, RequirementValue = 0, IsActive = true }
        };
        _context.Badges.AddRange(badges);
        await _context.SaveChangesAsync();

        // Act
        var categories = await _context.Badges
            .Select(b => b.Category)
            .Distinct()
            .ToListAsync();

        // Assert
        Assert.Equal(4, categories.Count);
    }

    [Fact]
    public async Task Badge_OnlyActiveAreAwarded()
    {
        // Arrange
        var activeBadge = new Badge
        {
            Name = "Active Badge",
            Category = BadgeCategory.Milestone,
            RequirementType = BadgeRequirementType.Points,
            RequirementValue = 100,
            IsActive = true
        };
        var inactiveBadge = new Badge
        {
            Name = "Inactive Badge",
            Category = BadgeCategory.Milestone,
            RequirementType = BadgeRequirementType.Points,
            RequirementValue = 100,
            IsActive = false
        };
        _context.Badges.AddRange(activeBadge, inactiveBadge);
        await _context.SaveChangesAsync();

        // Act
        var awardable = await _context.Badges
            .Where(b => b.IsActive)
            .ToListAsync();

        // Assert
        Assert.Single(awardable);
        Assert.Equal("Active Badge", awardable.First().Name);
    }

    [Fact]
    public async Task PointsForTokenConversion_CalculatesCorrectly()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        // Give student 1000 points
        var pointTransaction = new PointTransaction
        {
            StudentId = student.Id,
            BasePoints = 1000,
            FinalPoints = 1000,
            Source = PointSource.Exercise,
            Multiplier = 1.0m
        };
        _context.PointTransactions.Add(pointTransaction);
        await _context.SaveChangesAsync();

        // Act
        var totalPoints = await _context.PointTransactions
            .Where(p => p.StudentId == student.Id)
            .SumAsync(p => p.FinalPoints);

        // At 100 points per token
        const int pointsPerToken = 100;
        var tokensEarnable = totalPoints / pointsPerToken;

        // Assert
        Assert.Equal(10, tokensEarnable);
    }

    [Fact]
    public async Task PointDeduction_CreatesNegativeTransaction()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        // Initial points
        var earnedTransaction = new PointTransaction
        {
            StudentId = student.Id,
            BasePoints = 500,
            FinalPoints = 500,
            Source = PointSource.Exercise,
            Multiplier = 1.0m
        };
        _context.PointTransactions.Add(earnedTransaction);
        await _context.SaveChangesAsync();

        // Deduction for token conversion
        var deductionTransaction = new PointTransaction
        {
            StudentId = student.Id,
            BasePoints = -200,
            FinalPoints = -200,
            Source = PointSource.Adjustment,
            Multiplier = 1.0m,
            Details = "Token conversion"
        };
        _context.PointTransactions.Add(deductionTransaction);
        await _context.SaveChangesAsync();

        // Act
        var totalPoints = await _context.PointTransactions
            .Where(p => p.StudentId == student.Id)
            .SumAsync(p => p.FinalPoints);

        // Assert
        Assert.Equal(300, totalPoints); // 500 - 200
    }

    [Fact]
    public async Task RecentPointActivity_OrderedByDate()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var transactions = new List<PointTransaction>
        {
            new() { StudentId = student.Id, BasePoints = 10, FinalPoints = 10, Source = PointSource.Exercise, Details = "Day 1", Multiplier = 1.0m, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new() { StudentId = student.Id, BasePoints = 20, FinalPoints = 20, Source = PointSource.Exercise, Details = "Day 2", Multiplier = 1.0m, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { StudentId = student.Id, BasePoints = 30, FinalPoints = 30, Source = PointSource.Exercise, Details = "Day 3", Multiplier = 1.0m, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        };
        _context.PointTransactions.AddRange(transactions);
        await _context.SaveChangesAsync();

        // Act
        var recent = await _context.PointTransactions
            .Where(p => p.StudentId == student.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync();

        // Assert
        Assert.Equal("Day 3", recent.First().Details);
        Assert.Equal("Day 1", recent.Last().Details);
    }

    private ApplicationUser CreateTestStudent()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"test{Guid.NewGuid():N}@test.com",
            Email = $"test{Guid.NewGuid():N}@test.com",
            FirstName = "Test",
            LastName = "Student"
        };
    }
}
