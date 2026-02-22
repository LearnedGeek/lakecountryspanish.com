using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

public class ScheduleServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITicketService> _mockTicketService;
    private readonly Mock<ILogger<ScheduleService>> _loggerMock;
    private readonly ScheduleService _scheduleService;

    public ScheduleServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _mockTicketService = new Mock<ITicketService>();
        _loggerMock = new Mock<ILogger<ScheduleService>>();
        _scheduleService = new ScheduleService(_context, _mockTicketService.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAvailableTimeSlotsAsync_ReturnsOnlyActiveSlots()
    {
        // Arrange
        var activeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
        var inactiveSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Tuesday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsRecurring = true,
            IsActive = false
        };

        _context.TimeSlots.AddRange(activeSlot, inactiveSlot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _scheduleService.GetAvailableTimeSlotsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(DayOfWeek.Monday, result.First().DayOfWeek);
    }

    [Fact]
    public async Task HasOverlappingTimeSlotsAsync_ReturnsTrueForOverlap()
    {
        // Arrange
        var existingSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
        _context.TimeSlots.Add(existingSlot);
        await _context.SaveChangesAsync();

        // Act - Try to add overlapping slot (9:30 - 10:30)
        var hasOverlap = await _scheduleService.HasOverlappingTimeSlotsAsync(
            DayOfWeek.Monday,
            new TimeSpan(9, 30, 0),
            new TimeSpan(10, 30, 0));

        // Assert
        Assert.True(hasOverlap);
    }

    [Fact]
    public async Task HasOverlappingTimeSlotsAsync_ReturnsFalseForNoOverlap()
    {
        // Arrange
        var existingSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
        _context.TimeSlots.Add(existingSlot);
        await _context.SaveChangesAsync();

        // Act - Try to add non-overlapping slot (11:00 - 12:00)
        var hasOverlap = await _scheduleService.HasOverlappingTimeSlotsAsync(
            DayOfWeek.Monday,
            new TimeSpan(11, 0, 0),
            new TimeSpan(12, 0, 0));

        // Assert
        Assert.False(hasOverlap);
    }

    [Fact]
    public async Task CanDeleteTimeSlotAsync_ReturnsTrueWhenNoScheduledClasses()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        // Act
        var canDelete = await _scheduleService.CanDeleteTimeSlotAsync(timeSlot.Id);

        // Assert
        Assert.True(canDelete);
    }

    [Fact]
    public async Task CanDeleteTimeSlotAsync_ReturnsFalseWhenHasScheduledClasses()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var student = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "test@test.com",
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "Student"
        };
        _context.Users.Add(student);

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Now.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var canDelete = await _scheduleService.CanDeleteTimeSlotAsync(timeSlot.Id);

        // Assert
        Assert.False(canDelete);
    }

    [Fact]
    public async Task GetStudentClassesAsync_ReturnsOnlyStudentClasses()
    {
        // Arrange
        var student1 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "student1@test.com",
            Email = "student1@test.com",
            FirstName = "Student",
            LastName = "One"
        };
        var student2 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "student2@test.com",
            Email = "student2@test.com",
            FirstName = "Student",
            LastName = "Two"
        };
        _context.Users.AddRange(student1, student2);

        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.AddRange(
            new ScheduledClass
            {
                StudentId = student1.Id,
                TimeSlotId = timeSlot.Id,
                ClassDateTime = DateTime.Now.AddDays(1),
                Status = ClassStatus.Scheduled,
                PaymentStatus = PaymentStatus.Unpaid
            },
            new ScheduledClass
            {
                StudentId = student1.Id,
                TimeSlotId = timeSlot.Id,
                ClassDateTime = DateTime.Now.AddDays(8),
                Status = ClassStatus.Scheduled,
                PaymentStatus = PaymentStatus.Unpaid
            },
            new ScheduledClass
            {
                StudentId = student2.Id,
                TimeSlotId = timeSlot.Id,
                ClassDateTime = DateTime.Now.AddDays(2),
                Status = ClassStatus.Scheduled,
                PaymentStatus = PaymentStatus.Unpaid
            }
        );
        await _context.SaveChangesAsync();

        // Act
        var student1Classes = await _scheduleService.GetStudentClassesAsync(student1.Id);

        // Assert
        Assert.Equal(2, student1Classes.Count());
        Assert.All(student1Classes, c => Assert.Equal(student1.Id, c.StudentId));
    }

    #region Cancellation with Ticket Refund Tests

    [Fact]
    public async Task CancelClassWithForfeitAsync_PaidWithTicket_RefundsTicketWhenNotLate()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        // Class scheduled for 48 hours from now (not late cancellation)
        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(48),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PaidWithTicket,
            TicketId = 1
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        _mockTicketService.Setup(t => t.RefundTicketAsync(scheduledClass.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _scheduleService.CancelClassWithForfeitAsync(
            scheduledClass.Id, student.Id, isAdmin: false, acceptForfeit: false);

        // Assert
        Assert.True(result);
        var cancelledClass = await _context.ScheduledClasses.FindAsync(scheduledClass.Id);
        Assert.Equal(ClassStatus.Cancelled, cancelledClass!.Status);
        Assert.False(cancelledClass.CreditForfeited);
        _mockTicketService.Verify(t => t.RefundTicketAsync(scheduledClass.Id), Times.Once);
    }

    [Fact]
    public async Task CancelClassWithForfeitAsync_PaidWithTicket_ForfeitsTicketWhenLateCancellation()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        // Class scheduled for 12 hours from now (late cancellation - within 24 hours)
        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(12),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PaidWithTicket,
            TicketId = 1
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act - student cancelling late and accepting forfeit
        var result = await _scheduleService.CancelClassWithForfeitAsync(
            scheduledClass.Id, student.Id, isAdmin: false, acceptForfeit: true);

        // Assert
        Assert.True(result);
        var cancelledClass = await _context.ScheduledClasses.FindAsync(scheduledClass.Id);
        Assert.Equal(ClassStatus.Cancelled, cancelledClass!.Status);
        Assert.True(cancelledClass.CreditForfeited);
        // Ticket should NOT be refunded when forfeited
        _mockTicketService.Verify(t => t.RefundTicketAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CancelClassWithForfeitAsync_AdminCancel_RefundsTicketEvenWhenLate()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        // Class scheduled for 12 hours from now (would be late cancellation for student)
        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(12),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PaidWithTicket,
            TicketId = 1
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        _mockTicketService.Setup(t => t.RefundTicketAsync(scheduledClass.Id))
            .ReturnsAsync(true);

        // Act - admin cancelling
        var result = await _scheduleService.CancelClassWithForfeitAsync(
            scheduledClass.Id, "admin-id", isAdmin: true, acceptForfeit: false);

        // Assert
        Assert.True(result);
        var cancelledClass = await _context.ScheduledClasses.FindAsync(scheduledClass.Id);
        Assert.Equal(ClassStatus.Cancelled, cancelledClass!.Status);
        Assert.False(cancelledClass.CreditForfeited);
        _mockTicketService.Verify(t => t.RefundTicketAsync(scheduledClass.Id), Times.Once);
    }

    [Fact]
    public async Task CancelClassWithForfeitAsync_StudentCannotCancelAnotherStudentsClass()
    {
        // Arrange
        var student1 = CreateTestStudent();
        var student2 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "student2@test.com",
            Email = "student2@test.com",
            FirstName = "Student",
            LastName = "Two"
        };
        _context.Users.AddRange(student1, student2);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student1.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(48),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PaidWithTicket
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act - student2 trying to cancel student1's class
        var result = await _scheduleService.CancelClassWithForfeitAsync(
            scheduledClass.Id, student2.Id, isAdmin: false, acceptForfeit: false);

        // Assert
        Assert.False(result);
        var notCancelledClass = await _context.ScheduledClasses.FindAsync(scheduledClass.Id);
        Assert.Equal(ClassStatus.Scheduled, notCancelledClass!.Status);
    }

    [Fact]
    public async Task CancelClassWithForfeitAsync_CannotCancelAlreadyCancelledClass()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(48),
            Status = ClassStatus.Cancelled, // Already cancelled
            PaymentStatus = PaymentStatus.PaidWithTicket
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var result = await _scheduleService.CancelClassWithForfeitAsync(
            scheduledClass.Id, student.Id, isAdmin: false, acceptForfeit: false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CancelClassWithForfeitAsync_StudentMustAcceptForfeitForLateCancellation()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        // Class scheduled for 12 hours from now (late cancellation)
        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(12),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PaidWithTicket
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act - student trying to cancel late without accepting forfeit
        var result = await _scheduleService.CancelClassWithForfeitAsync(
            scheduledClass.Id, student.Id, isAdmin: false, acceptForfeit: false);

        // Assert - should fail because forfeit not accepted
        Assert.False(result);
        var notCancelledClass = await _context.ScheduledClasses.FindAsync(scheduledClass.Id);
        Assert.Equal(ClassStatus.Scheduled, notCancelledClass!.Status);
    }

    #endregion

    #region CanBookClass Tests

    [Fact]
    public void CanBookClass_ReturnsFalse_WhenWithin24Hours()
    {
        var classDateTime = DateTime.Now.AddHours(12);

        var result = _scheduleService.CanBookClass(classDateTime);

        Assert.False(result);
    }

    [Fact]
    public void CanBookClass_ReturnsTrue_WhenBeyond24Hours()
    {
        var classDateTime = DateTime.Now.AddHours(48);

        var result = _scheduleService.CanBookClass(classDateTime);

        Assert.True(result);
    }

    [Fact]
    public void CanBookClass_ReturnsFalse_WhenInPast()
    {
        var classDateTime = DateTime.Now.AddHours(-1);

        var result = _scheduleService.CanBookClass(classDateTime);

        Assert.False(result);
    }

    #endregion

    #region GetCancellationStatusAsync Tests

    [Fact]
    public async Task GetCancellationStatus_CanCancel_WhenOutside24Hours()
    {
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(48),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PaidWithTicket
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var (canCancel, willForfeit) = await _scheduleService.GetCancellationStatusAsync(scheduledClass.Id);

        Assert.True(canCancel);
        Assert.False(willForfeit);
    }

    [Fact]
    public async Task GetCancellationStatus_WillForfeit_WhenWithin24Hours()
    {
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(12),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PaidWithTicket
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var (canCancel, willForfeit) = await _scheduleService.GetCancellationStatusAsync(scheduledClass.Id);

        Assert.True(canCancel);
        Assert.True(willForfeit);
    }

    #endregion

    #region BookRecurringClassesAsync Tests

    [Fact]
    public async Task BookRecurringClasses_CreatesMultipleClasses()
    {
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);

        var package = new Package { Name = "Test", Description = "Test", ClassCount = 10, Price = 100m };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        _context.StudentPackages.Add(new StudentPackage
        {
            StudentId = student.Id,
            PackageId = package.Id,
            ClassesRemaining = 10,
            PurchaseDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Start 2 weeks out to avoid 24-hour booking cutoff
        var startDate = GetNextDayOfWeek(DayOfWeek.Monday).AddDays(7);

        var result = await _scheduleService.BookRecurringClassesAsync(student.Id, timeSlot.Id, startDate, 3);

        Assert.True(result.Success);
        Assert.Equal(3, result.BookedClasses.Count);
    }

    [Fact]
    public async Task BookRecurringClasses_FailsWithInsufficientCredits()
    {
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var startDate = GetNextDayOfWeek(DayOfWeek.Monday);

        // No StudentPackage = no credits
        var result = await _scheduleService.BookRecurringClassesAsync(student.Id, timeSlot.Id, startDate, 3);

        Assert.False(result.Success);
        Assert.NotEmpty(result.ConflictReasons);
    }

    #endregion

    #region BookClassAsync Tests

    [Fact]
    public async Task BookClassAsync_WithAvailableSlot_CreatesScheduledClass()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var classDateTime = GetNextDayOfWeek(DayOfWeek.Monday).Add(timeSlot.StartTime);

        // Act
        var result = await _scheduleService.BookClassAsync(student.Id, timeSlot.Id, classDateTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(student.Id, result.StudentId);
        Assert.Equal(timeSlot.Id, result.TimeSlotId);
        Assert.Equal(classDateTime, result.ClassDateTime);
        Assert.Equal(ClassStatus.Scheduled, result.Status);
        Assert.Equal(PaymentStatus.Unpaid, result.PaymentStatus);
    }

    [Fact]
    public async Task BookClassAsync_WithAlreadyBookedSlot_ReturnsNull()
    {
        // Arrange
        var student1 = CreateTestStudent();
        var student2 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "student2@test.com",
            Email = "student2@test.com",
            FirstName = "Student",
            LastName = "Two"
        };
        _context.Users.AddRange(student1, student2);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var classDateTime = GetNextDayOfWeek(DayOfWeek.Monday).Add(timeSlot.StartTime);

        // Book the class first
        var existingClass = new ScheduledClass
        {
            StudentId = student1.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = classDateTime,
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid
        };
        _context.ScheduledClasses.Add(existingClass);
        await _context.SaveChangesAsync();

        // Act - Try to book same slot
        var result = await _scheduleService.BookClassAsync(student2.Id, timeSlot.Id, classDateTime);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Helper Methods

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

    private DateTime GetNextDayOfWeek(DayOfWeek dayOfWeek)
    {
        var today = DateTime.Today;
        var daysUntil = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
        if (daysUntil == 0) daysUntil = 7; // Get next week if today is that day
        return today.AddDays(daysUntil);
    }

    #endregion
}
