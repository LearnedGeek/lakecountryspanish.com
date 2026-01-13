using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Tests.Services;

/// <summary>
/// Tests for subscription-related data operations.
/// </summary>
public class SubscriptionServiceTests
{
    private readonly ApplicationDbContext _context;

    public SubscriptionServiceTests()
    {
        _context = TestDbContextFactory.Create();
    }

    [Fact]
    public async Task Subscription_WhenActive_IsRetrievable()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tier = new SubscriptionTier
        {
            Name = "Monthly",
            MonthlyPrice = 49.99m,
            ClassesPerMonth = 4,
            PointsMultiplier = 1.5m,
            IsActive = true,
            DisplayOrder = 1
        };
        _context.SubscriptionTiers.Add(tier);
        await _context.SaveChangesAsync();

        var subscription = new Subscription
        {
            StudentId = student.Id,
            SubscriptionTierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            ClassesUsedThisMonth = 0
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.Subscriptions
            .Include(s => s.Tier)
            .FirstOrDefaultAsync(s => s.StudentId == student.Id && s.Status == SubscriptionStatus.Active);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SubscriptionStatus.Active, result.Status);
        Assert.Equal(4, result.ClassesRemainingThisMonth); // 4 - 0 used
        Assert.Equal(49.99m, result.Tier?.MonthlyPrice);
    }

    [Fact]
    public async Task SubscriptionTier_OnlyActiveTiersReturned()
    {
        // Arrange
        var activeTier = new SubscriptionTier
        {
            Name = "Basic",
            MonthlyPrice = 29.99m,
            ClassesPerMonth = 2,
            PointsMultiplier = 1.0m,
            IsActive = true,
            DisplayOrder = 1
        };
        var inactiveTier = new SubscriptionTier
        {
            Name = "Legacy",
            MonthlyPrice = 19.99m,
            ClassesPerMonth = 1,
            PointsMultiplier = 1.0m,
            IsActive = false,
            DisplayOrder = 2
        };
        _context.SubscriptionTiers.AddRange(activeTier, inactiveTier);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.SubscriptionTiers
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Basic", result.First().Name);
    }

    [Fact]
    public async Task Subscription_CancellationRequest_SetsCorrectFields()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tier = new SubscriptionTier
        {
            Name = "Monthly",
            MonthlyPrice = 49.99m,
            ClassesPerMonth = 4,
            PointsMultiplier = 1.5m,
            IsActive = true
        };
        _context.SubscriptionTiers.Add(tier);
        await _context.SaveChangesAsync();

        var subscription = new Subscription
        {
            StudentId = student.Id,
            SubscriptionTierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act - Simulate cancellation request
        subscription.CancellationRequested = true;
        subscription.CancellationRequestDate = DateTime.UtcNow;
        subscription.EffectiveCancellationDate = subscription.CurrentPeriodEnd;
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Subscriptions.FindAsync(subscription.Id);
        Assert.True(updated?.CancellationRequested);
        Assert.NotNull(updated?.CancellationRequestDate);
        Assert.NotNull(updated?.EffectiveCancellationDate);
    }

    [Fact]
    public async Task Subscription_WithTier_IncludesPointsMultiplier()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var premiumTier = new SubscriptionTier
        {
            Name = "Premium",
            MonthlyPrice = 79.99m,
            ClassesPerMonth = 8,
            PointsMultiplier = 2.0m, // 2x points
            IsActive = true
        };
        _context.SubscriptionTiers.Add(premiumTier);
        await _context.SaveChangesAsync();

        var subscription = new Subscription
        {
            StudentId = student.Id,
            SubscriptionTierId = premiumTier.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.Subscriptions
            .Include(s => s.Tier)
            .FirstOrDefaultAsync(s => s.Id == subscription.Id);

        // Assert
        Assert.Equal(2.0m, result?.Tier?.PointsMultiplier);
    }

    [Fact]
    public async Task RecurringSchedule_AssociatedWithSubscription()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tier = new SubscriptionTier
        {
            Name = "Monthly",
            MonthlyPrice = 49.99m,
            ClassesPerMonth = 4,
            PointsMultiplier = 1.5m,
            IsActive = true
        };
        _context.SubscriptionTiers.Add(tier);

        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var subscription = new Subscription
        {
            StudentId = student.Id,
            SubscriptionTierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        var recurringSchedule = new RecurringSchedule
        {
            SubscriptionId = subscription.Id,
            TimeSlotId = timeSlot.Id,
            DayOfWeek = DayOfWeek.Monday,
            IsActive = true
        };
        _context.RecurringSchedules.Add(recurringSchedule);
        await _context.SaveChangesAsync();

        // Act
        var schedules = await _context.RecurringSchedules
            .Where(rs => rs.SubscriptionId == subscription.Id && rs.IsActive)
            .Include(rs => rs.TimeSlot)
            .ToListAsync();

        // Assert
        Assert.Single(schedules);
        Assert.Equal(DayOfWeek.Monday, schedules.First().DayOfWeek);
        Assert.Equal(new TimeSpan(10, 0, 0), schedules.First().TimeSlot?.StartTime);
    }

    [Fact]
    public async Task Subscription_ClassUsageIncrement_Works()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tier = new SubscriptionTier
        {
            Name = "Monthly",
            MonthlyPrice = 49.99m,
            ClassesPerMonth = 4,
            PointsMultiplier = 1.5m,
            IsActive = true
        };
        _context.SubscriptionTiers.Add(tier);
        await _context.SaveChangesAsync();

        var subscription = new Subscription
        {
            StudentId = student.Id,
            SubscriptionTierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            ClassesUsedThisMonth = 0
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act - Use one class
        subscription.ClassesUsedThisMonth++;
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Subscriptions
            .Include(s => s.Tier)
            .FirstAsync(s => s.Id == subscription.Id);
        Assert.Equal(1, updated.ClassesUsedThisMonth);
        Assert.Equal(3, updated.ClassesRemainingThisMonth); // 4 - 1 = 3
    }

    [Fact]
    public async Task Subscription_MultipleStatusTransitions()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tier = new SubscriptionTier
        {
            Name = "Monthly",
            MonthlyPrice = 49.99m,
            ClassesPerMonth = 4,
            PointsMultiplier = 1.5m,
            IsActive = true
        };
        _context.SubscriptionTiers.Add(tier);
        await _context.SaveChangesAsync();

        var subscription = new Subscription
        {
            StudentId = student.Id,
            SubscriptionTierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act - Transition through states
        subscription.Status = SubscriptionStatus.Paused;
        subscription.PausedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var pausedSub = await _context.Subscriptions.FindAsync(subscription.Id);
        Assert.Equal(SubscriptionStatus.Paused, pausedSub?.Status);

        // Resume
        subscription.Status = SubscriptionStatus.Active;
        subscription.PausedAt = null;
        await _context.SaveChangesAsync();

        var resumedSub = await _context.Subscriptions.FindAsync(subscription.Id);
        Assert.Equal(SubscriptionStatus.Active, resumedSub?.Status);
    }

    [Fact]
    public async Task Subscription_PastDueStatus()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tier = new SubscriptionTier
        {
            Name = "Monthly",
            MonthlyPrice = 49.99m,
            ClassesPerMonth = 4,
            PointsMultiplier = 1.5m,
            IsActive = true
        };
        _context.SubscriptionTiers.Add(tier);
        await _context.SaveChangesAsync();

        var subscription = new Subscription
        {
            StudentId = student.Id,
            SubscriptionTierId = tier.Id,
            Status = SubscriptionStatus.PastDue,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-30),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(-15) // Past due
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act
        var pastDue = await _context.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.PastDue)
            .ToListAsync();

        // Assert
        Assert.Single(pastDue);
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
