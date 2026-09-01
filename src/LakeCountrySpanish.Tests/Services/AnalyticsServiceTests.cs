using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

public class AnalyticsServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AnalyticsService _service;

    public AnalyticsServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _service = new AnalyticsService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetDashboardMetricsAsync Tests

    [Fact]
    public async Task GetDashboardMetrics_ReturnsZeros_WhenEmpty()
    {
        var metrics = await _service.GetDashboardMetricsAsync();

        Assert.Equal(0, metrics.ActiveStudents);
        Assert.Equal(0, metrics.TotalStudents);
        Assert.Equal(0m, metrics.MRR);
        Assert.Equal(0, metrics.ClassesThisMonth);
    }

    [Fact]
    public async Task GetDashboardMetrics_CountsActiveStudents()
    {
        var student = CreateTestStudent("active@test.com");
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(-5),
            Status = ClassStatus.Completed,
            PaymentStatus = PaymentStatus.Paid
        });

        _context.PointTransactions.Add(new PointTransaction
        {
            StudentId = student.Id,
            Source = PointSource.ClassCompletion,
            BasePoints = 10,
            Multiplier = 1.0m,
            FinalPoints = 10,
            BalanceAfter = 10,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });
        await _context.SaveChangesAsync();

        var metrics = await _service.GetDashboardMetricsAsync();

        Assert.True(metrics.TotalStudents >= 1);
        Assert.True(metrics.ActiveStudents >= 1);
    }

    [Fact]
    public async Task GetDashboardMetrics_CountsClassesThisMonth()
    {
        var student = CreateTestStudent("class@test.com");
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            // 1 hour ago rather than 3 days ago — a fixed-offset in-the-past
            // date crosses the month boundary in early September / early any
            // month, which used to fail this "classes this month" assertion
            // on Sept 1. 1 hour is always the same day, so always the same
            // month regardless of when CI runs.
            ClassDateTime = DateTime.UtcNow.AddHours(-1),
            Status = ClassStatus.Completed,
            PaymentStatus = PaymentStatus.Paid
        });
        await _context.SaveChangesAsync();

        var metrics = await _service.GetDashboardMetricsAsync();

        Assert.Equal(1, metrics.ClassesThisMonth);
    }

    #endregion

    #region GetDailyRevenueAsync Tests

    [Fact]
    public async Task GetDailyRevenue_FillsGaps_WithZeros()
    {
        var startDate = DateTime.Today.AddDays(-5);
        var endDate = DateTime.Today; // exclusive — produces 5 entries

        var result = (await _service.GetDailyRevenueAsync(startDate, endDate)).ToList();

        Assert.Equal(5, result.Count);
        Assert.All(result, d => Assert.Equal(0m, d.TotalRevenue));
    }

    [Fact]
    public async Task GetDailyRevenue_IncludesPaymentData()
    {
        var student = CreateTestStudent("revenue@test.com");
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        _context.Payments.Add(new Payment
        {
            StudentId = student.Id,
            Amount = 45.00m,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.SingleClass,
            CreatedAt = DateTime.Today.AddDays(-2),
            CompletedAt = DateTime.Today.AddDays(-2)
        });
        await _context.SaveChangesAsync();

        var startDate = DateTime.Today.AddDays(-5);
        var endDate = DateTime.Today; // exclusive
        var result = (await _service.GetDailyRevenueAsync(startDate, endDate)).ToList();

        Assert.Equal(5, result.Count);
        var dayWithRevenue = result.FirstOrDefault(d => d.Date.Date == DateTime.Today.AddDays(-2).Date);
        Assert.NotNull(dayWithRevenue);
        Assert.True(dayWithRevenue.TotalRevenue > 0);
    }

    #endregion

    #region GetMonthlyRevenueAsync Tests

    [Fact]
    public async Task GetMonthlyRevenue_ReturnsRequestedMonths()
    {
        var result = (await _service.GetMonthlyRevenueAsync(6)).ToList();

        Assert.Equal(6, result.Count);
    }

    [Fact]
    public async Task GetMonthlyRevenue_FillsGaps_WithZeros()
    {
        var result = (await _service.GetMonthlyRevenueAsync(3)).ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, m => Assert.Equal(0m, m.TotalRevenue));
    }

    #endregion

    #region GetTopEngagedStudentsAsync Tests

    [Fact]
    public async Task GetTopEngagedStudents_ReturnsByPoints()
    {
        var student1 = CreateTestStudent("top1@test.com");
        var student2 = CreateTestStudent("top2@test.com");
        _context.Users.AddRange(student1, student2);
        await _context.SaveChangesAsync();

        _context.PointTransactions.Add(new PointTransaction
        {
            StudentId = student1.Id,
            Source = PointSource.ClassCompletion,
            BasePoints = 100,
            Multiplier = 1.0m,
            FinalPoints = 100,
            BalanceAfter = 100,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        _context.PointTransactions.Add(new PointTransaction
        {
            StudentId = student2.Id,
            Source = PointSource.ClassCompletion,
            BasePoints = 50,
            Multiplier = 1.0m,
            FinalPoints = 50,
            BalanceAfter = 50,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var result = (await _service.GetTopEngagedStudentsAsync(10)).ToList();

        Assert.True(result.Count >= 2);
        Assert.Equal(student1.Id, result.First().StudentId);
    }

    #endregion

    #region GetAtRiskStudentsAsync Tests

    [Fact]
    public async Task GetAtRiskStudents_ReturnsInactiveStudents()
    {
        var activeStudent = CreateTestStudent("active@test.com");
        var atRiskStudent = CreateTestStudent("atrisk@test.com");
        _context.Users.AddRange(activeStudent, atRiskStudent);
        await _context.SaveChangesAsync();

        // Active student - recent activity
        _context.PointTransactions.Add(new PointTransaction
        {
            StudentId = activeStudent.Id,
            Source = PointSource.ClassCompletion,
            BasePoints = 10,
            Multiplier = 1.0m,
            FinalPoints = 10,
            BalanceAfter = 10,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        // At-risk student - activity 20 days ago (> 14 day threshold)
        _context.PointTransactions.Add(new PointTransaction
        {
            StudentId = atRiskStudent.Id,
            Source = PointSource.ClassCompletion,
            BasePoints = 10,
            Multiplier = 1.0m,
            FinalPoints = 10,
            BalanceAfter = 10,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        });
        await _context.SaveChangesAsync();

        var result = (await _service.GetAtRiskStudentsAsync(10)).ToList();

        Assert.Single(result);
        Assert.Equal(atRiskStudent.Id, result.First().StudentId);
    }

    #endregion

    #region GetRevenueMetricsAsync Tests

    [Fact]
    public async Task GetRevenueMetrics_ReturnsZeros_WhenNoPayments()
    {
        var startDate = DateTime.Today.AddDays(-30);
        var endDate = DateTime.Today;

        var metrics = await _service.GetRevenueMetricsAsync(startDate, endDate);

        Assert.Equal(0m, metrics.TotalRevenue);
        Assert.Equal(0m, metrics.SubscriptionRevenue);
        Assert.Equal(0m, metrics.TokenRevenue);
    }

    [Fact]
    public async Task GetRevenueMetrics_BreaksDownByType()
    {
        var student = CreateTestStudent("rev@test.com");
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        _context.Payments.Add(new Payment
        {
            StudentId = student.Id,
            Amount = 100.00m,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.Subscription,
            CreatedAt = DateTime.Today.AddDays(-5),
            CompletedAt = DateTime.Today.AddDays(-5)
        });
        _context.Payments.Add(new Payment
        {
            StudentId = student.Id,
            Amount = 25.00m,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.TokenPurchase,
            CreatedAt = DateTime.Today.AddDays(-3),
            CompletedAt = DateTime.Today.AddDays(-3)
        });
        await _context.SaveChangesAsync();

        var startDate = DateTime.Today.AddDays(-30);
        var endDate = DateTime.Today;
        var metrics = await _service.GetRevenueMetricsAsync(startDate, endDate);

        Assert.Equal(125.00m, metrics.TotalRevenue);
        Assert.Equal(100.00m, metrics.SubscriptionRevenue);
        Assert.Equal(25.00m, metrics.TokenRevenue);
    }

    #endregion

    #region GetSubscriptionMetricsAsync Tests

    [Fact]
    public async Task GetSubscriptionMetrics_CountsByStatus()
    {
        var student1 = CreateTestStudent("sub1@test.com");
        var student2 = CreateTestStudent("sub2@test.com");
        _context.Users.AddRange(student1, student2);

        var tier = new SubscriptionTier
        {
            Name = "Basic",
            ClassesPerMonth = 4,
            MonthlyPrice = 100m,
            IsActive = true,
            DisplayOrder = 1
        };
        _context.SubscriptionTiers.Add(tier);
        await _context.SaveChangesAsync();

        _context.Subscriptions.Add(new Subscription
        {
            StudentId = student1.Id,
            SubscriptionTierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            CreatedAt = DateTime.UtcNow.AddMonths(-2)
        });
        _context.Subscriptions.Add(new Subscription
        {
            StudentId = student2.Id,
            SubscriptionTierId = tier.Id,
            Status = SubscriptionStatus.Paused,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            PausedAt = DateTime.UtcNow.AddDays(-5),
            CreatedAt = DateTime.UtcNow.AddMonths(-1)
        });
        await _context.SaveChangesAsync();

        var metrics = await _service.GetSubscriptionMetricsAsync();

        Assert.Equal(1, metrics.ActiveCount);
        Assert.Equal(1, metrics.PausedCount);
    }

    #endregion

    #region Helper Methods

    private ApplicationUser CreateTestStudent(string email)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "Student"
        };
    }

    private TimeSlot CreateTestTimeSlot()
    {
        return new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
    }

    #endregion
}
