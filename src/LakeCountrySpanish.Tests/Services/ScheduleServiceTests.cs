using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

public class ScheduleServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly ScheduleService _scheduleService;

    public ScheduleServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _scheduleService = new ScheduleService(_context);
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
}
