using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

public class ClassSchedulingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IScheduleService> _scheduleServiceMock;
    private readonly Mock<IPaymentService> _paymentServiceMock;
    private readonly Mock<ITicketService> _ticketServiceMock;
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly Mock<ILogger<ClassSchedulingService>> _loggerMock;
    private readonly ClassSchedulingService _service;
    private readonly string _studentId;

    public ClassSchedulingServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _configMock = new Mock<IConfiguration>();
        _scheduleServiceMock = new Mock<IScheduleService>();
        _paymentServiceMock = new Mock<IPaymentService>();
        _ticketServiceMock = new Mock<ITicketService>();
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
        _loggerMock = new Mock<ILogger<ClassSchedulingService>>();

        _paymentServiceMock.Setup(p => p.GetClassPriceForStudentAsync(It.IsAny<string>()))
            .ReturnsAsync(45.00m);

        _service = new ClassSchedulingService(
            _context,
            _configMock.Object,
            _scheduleServiceMock.Object,
            _paymentServiceMock.Object,
            _ticketServiceMock.Object,
            _subscriptionServiceMock.Object,
            _loggerMock.Object);

        _studentId = Guid.NewGuid().ToString();
        _context.Users.Add(new ApplicationUser
        {
            Id = _studentId,
            UserName = "student@test.com",
            Email = "student@test.com",
            FirstName = "Test",
            LastName = "Student"
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region CleanupStalePendingClassesAsync Tests

    [Fact]
    public async Task CleanupStalePendingClasses_RemovesStalePendingClasses()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPendingCheckout = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-60) // 60 minutes old
        });
        await _context.SaveChangesAsync();

        var removed = await _service.CleanupStalePendingClassesAsync(30);

        Assert.Equal(1, removed);
        Assert.Empty(_context.ScheduledClasses);
    }

    [Fact]
    public async Task CleanupStalePendingClasses_IgnoresRecentPendingClasses()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPendingCheckout = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10) // Only 10 minutes old
        });
        await _context.SaveChangesAsync();

        var removed = await _service.CleanupStalePendingClassesAsync(30);

        Assert.Equal(0, removed);
        Assert.Single(_context.ScheduledClasses);
    }

    [Fact]
    public async Task CleanupStalePendingClasses_IgnoresNonPendingClasses()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Paid,
            IsPendingCheckout = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-60)
        });
        await _context.SaveChangesAsync();

        var removed = await _service.CleanupStalePendingClassesAsync(30);

        Assert.Equal(0, removed);
        Assert.Single(_context.ScheduledClasses);
    }

    #endregion

    #region RemoveFromCheckoutAsync Tests

    [Fact]
    public async Task RemoveFromCheckout_RemovesPendingClass()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPendingCheckout = true
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var result = await _service.RemoveFromCheckoutAsync(scheduledClass.Id, _studentId);

        Assert.True(result);
        Assert.Empty(_context.ScheduledClasses);
    }

    [Fact]
    public async Task RemoveFromCheckout_ReturnsFalse_WhenClassNotFound()
    {
        var result = await _service.RemoveFromCheckoutAsync(999, _studentId);

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveFromCheckout_ReturnsFalse_WhenNotOwner()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPendingCheckout = true
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var result = await _service.RemoveFromCheckoutAsync(scheduledClass.Id, "different-user");

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveFromCheckout_ReturnsFalse_WhenAlreadyPaid()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Paid,
            IsPendingCheckout = false
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var result = await _service.RemoveFromCheckoutAsync(scheduledClass.Id, _studentId);

        Assert.False(result);
    }

    #endregion

    #region ClearPendingCheckoutAsync Tests

    [Fact]
    public async Task ClearPendingCheckout_ClearsAllPendingClasses()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 3; i++)
        {
            _context.ScheduledClasses.Add(new ScheduledClass
            {
                StudentId = _studentId,
                TimeSlotId = timeSlot.Id,
                ClassDateTime = DateTime.UtcNow.AddDays(7 + i),
                Status = ClassStatus.Scheduled,
                PaymentStatus = PaymentStatus.Unpaid,
                IsPendingCheckout = true
            });
        }
        await _context.SaveChangesAsync();

        var removed = await _service.ClearPendingCheckoutAsync(_studentId);

        Assert.Equal(3, removed);
        Assert.Empty(_context.ScheduledClasses);
    }

    [Fact]
    public async Task ClearPendingCheckout_PreservesPaidClasses()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPendingCheckout = true
        });
        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(14),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Paid,
            IsPendingCheckout = false
        });
        await _context.SaveChangesAsync();

        var removed = await _service.ClearPendingCheckoutAsync(_studentId);

        Assert.Equal(1, removed);
        Assert.Single(_context.ScheduledClasses);
        Assert.Equal(PaymentStatus.Paid, _context.ScheduledClasses.First().PaymentStatus);
    }

    #endregion

    #region GetPendingCheckoutAsync Tests

    [Fact]
    public async Task GetPendingCheckout_ReturnsStudentPendingClasses()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = _studentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPendingCheckout = true
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetPendingCheckoutAsync(_studentId);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetPendingCheckout_ExcludesOtherStudentClasses()
    {
        var otherStudentId = Guid.NewGuid().ToString();
        _context.Users.Add(new ApplicationUser
        {
            Id = otherStudentId,
            UserName = "other@test.com",
            Email = "other@test.com",
            FirstName = "Other",
            LastName = "Student"
        });

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = otherStudentId,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPendingCheckout = true
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetPendingCheckoutAsync(_studentId);

        Assert.Empty(result);
    }

    #endregion

    #region GetCheckoutSummaryAsync Tests

    [Fact]
    public async Task GetCheckoutSummary_CalculatesCorrectTotals()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 2; i++)
        {
            _context.ScheduledClasses.Add(new ScheduledClass
            {
                StudentId = _studentId,
                TimeSlotId = timeSlot.Id,
                ClassDateTime = DateTime.UtcNow.AddDays(7 + i),
                Status = ClassStatus.Scheduled,
                PaymentStatus = PaymentStatus.Unpaid,
                IsPendingCheckout = true
            });
        }
        await _context.SaveChangesAsync();

        _ticketServiceMock.Setup(t => t.GetAvailableTicketCountAsync(_studentId))
            .ReturnsAsync(0);

        var summary = await _service.GetCheckoutSummaryAsync(_studentId);

        Assert.Equal(2, summary.ClassCount);
        Assert.Equal(45.00m, summary.PerClassPrice);
        Assert.Equal(90.00m, summary.Subtotal);
        Assert.Equal(0, summary.TicketsApplied);
        Assert.Equal(90.00m, summary.TotalDue);
    }

    [Fact]
    public async Task GetCheckoutSummary_AppliesTicketDiscount()
    {
        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 3; i++)
        {
            _context.ScheduledClasses.Add(new ScheduledClass
            {
                StudentId = _studentId,
                TimeSlotId = timeSlot.Id,
                ClassDateTime = DateTime.UtcNow.AddDays(7 + i),
                Status = ClassStatus.Scheduled,
                PaymentStatus = PaymentStatus.Unpaid,
                IsPendingCheckout = true
            });
        }
        await _context.SaveChangesAsync();

        _ticketServiceMock.Setup(t => t.GetAvailableTicketCountAsync(_studentId))
            .ReturnsAsync(5);

        var summary = await _service.GetCheckoutSummaryAsync(_studentId, ticketsToApply: 2);

        Assert.Equal(3, summary.ClassCount);
        Assert.Equal(2, summary.TicketsApplied);
        Assert.Equal(45.00m, summary.TotalDue); // 3 classes - 2 tickets = 1 class @ $45
    }

    [Fact]
    public async Task GetCheckoutSummary_ReturnsZero_WhenNoPendingClasses()
    {
        _ticketServiceMock.Setup(t => t.GetAvailableTicketCountAsync(_studentId))
            .ReturnsAsync(0);

        var summary = await _service.GetCheckoutSummaryAsync(_studentId);

        Assert.Equal(0, summary.ClassCount);
        Assert.Equal(0m, summary.TotalDue);
    }

    #endregion

    #region Helper Methods

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
