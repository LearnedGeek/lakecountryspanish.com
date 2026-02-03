using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Controllers;

/// <summary>
/// Tests for ScheduledTasksController.
/// Verifies scheduled task endpoints and admin notification behavior.
/// </summary>
public class ScheduledTasksControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IClassSchedulingService> _classSchedulingServiceMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<ScheduledTasksController>> _loggerMock;
    private readonly ScheduledTasksController _controller;
    private readonly ApplicationUser _testStudent;

    public ScheduledTasksControllerTests()
    {
        _context = TestDbContextFactory.Create();
        _emailServiceMock = new Mock<IEmailService>();
        _classSchedulingServiceMock = new Mock<IClassSchedulingService>();
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<ScheduledTasksController>>();

        // Setup configuration
        _configMock.Setup(c => c["ScheduledTasks:SecretKey"]).Returns("test-secret-key");

        _controller = new ScheduledTasksController(
            _context,
            _emailServiceMock.Object,
            _classSchedulingServiceMock.Object,
            _configMock.Object,
            _loggerMock.Object);

        // Setup HttpContext with request headers
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Setup test student
        _testStudent = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "teststudent@test.com",
            Email = "teststudent@test.com",
            FirstName = "Test",
            LastName = "Student",
            ClassroomUrl = "https://meet.google.com/test"
        };
        _context.Users.Add(_testStudent);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Authentication Tests

    [Fact]
    public async Task SendClassReminders_ReturnsUnauthorized_WhenNoKeyProvided()
    {
        // Act
        var result = await _controller.SendClassReminders(null);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SendClassReminders_ReturnsUnauthorized_WhenInvalidKeyProvided()
    {
        // Act
        var result = await _controller.SendClassReminders("wrong-key");

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SendClassReminders_AcceptsValidKey_FromQueryParameter()
    {
        // Act
        var result = await _controller.SendClassReminders("test-secret-key");

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SendClassReminders_AcceptsValidKey_FromHeader()
    {
        // Arrange
        _controller.HttpContext.Request.Headers["X-Task-Key"] = "test-secret-key";

        // Act
        var result = await _controller.SendClassReminders(null);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    #endregion

    #region Class Reminder Tests

    [Fact]
    public async Task SendClassReminders_SendsBothStudentAndAdminReminders_For24HourWindow()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var classDateTime = DateTime.UtcNow.AddHours(24);
        var scheduledClass = new ScheduledClass
        {
            StudentId = _testStudent.Id,
            Student = _testStudent,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = classDateTime,
            Status = ClassStatus.Scheduled,
            Reminder24HrSent = false,
            Reminder1HrSent = false
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SendClassReminders("test-secret-key");

        // Assert - Student reminder
        _emailServiceMock.Verify(
            e => e.SendClassReminderAsync(
                _testStudent.Email!,
                It.IsAny<string>(),
                classDateTime,
                _testStudent.ClassroomUrl,
                24),
            Times.Once);

        // Assert - Admin reminder
        _emailServiceMock.Verify(
            e => e.SendAdminClassReminderAsync(
                It.IsAny<string>(),
                _testStudent.Email!,
                classDateTime,
                24),
            Times.Once);
    }

    [Fact]
    public async Task SendClassReminders_SendsBothStudentAndAdminReminders_For1HourWindow()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var classDateTime = DateTime.UtcNow.AddHours(1);
        var scheduledClass = new ScheduledClass
        {
            StudentId = _testStudent.Id,
            Student = _testStudent,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = classDateTime,
            Status = ClassStatus.Scheduled,
            Reminder24HrSent = true, // Already sent
            Reminder1HrSent = false
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SendClassReminders("test-secret-key");

        // Assert - Student reminder
        _emailServiceMock.Verify(
            e => e.SendClassReminderAsync(
                _testStudent.Email!,
                It.IsAny<string>(),
                classDateTime,
                _testStudent.ClassroomUrl,
                1),
            Times.Once);

        // Assert - Admin reminder
        _emailServiceMock.Verify(
            e => e.SendAdminClassReminderAsync(
                It.IsAny<string>(),
                _testStudent.Email!,
                classDateTime,
                1),
            Times.Once);
    }

    [Fact]
    public async Task SendClassReminders_DoesNotSendReminders_ForCancelledClasses()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = _testStudent.Id,
            Student = _testStudent,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(24),
            Status = ClassStatus.Cancelled, // Cancelled
            Reminder24HrSent = false,
            Reminder1HrSent = false
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SendClassReminders("test-secret-key");

        // Assert - No reminders sent
        _emailServiceMock.Verify(
            e => e.SendClassReminderAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<int>()),
            Times.Never);

        _emailServiceMock.Verify(
            e => e.SendAdminClassReminderAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task SendClassReminders_DoesNotResendReminders_WhenAlreadySent()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = _testStudent.Id,
            Student = _testStudent,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(24),
            Status = ClassStatus.Scheduled,
            Reminder24HrSent = true, // Already sent
            Reminder1HrSent = true   // Already sent
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SendClassReminders("test-secret-key");

        // Assert - No reminders sent
        _emailServiceMock.Verify(
            e => e.SendClassReminderAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<int>()),
            Times.Never);

        _emailServiceMock.Verify(
            e => e.SendAdminClassReminderAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task SendClassReminders_UpdatesReminderSentFlags()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = _testStudent.Id,
            Student = _testStudent,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(24),
            Status = ClassStatus.Scheduled,
            Reminder24HrSent = false,
            Reminder1HrSent = false
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        await _controller.SendClassReminders("test-secret-key");

        // Assert - Refresh from database
        await _context.Entry(scheduledClass).ReloadAsync();
        Assert.True(scheduledClass.Reminder24HrSent);
    }

    [Fact]
    public async Task SendClassReminders_ReturnsCorrectCount()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        // Add two classes needing 24-hour reminders
        for (int i = 0; i < 2; i++)
        {
            var student = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = $"student{i}@test.com",
                Email = $"student{i}@test.com",
                FirstName = $"Student{i}"
            };
            _context.Users.Add(student);

            _context.ScheduledClasses.Add(new ScheduledClass
            {
                StudentId = student.Id,
                Student = student,
                TimeSlotId = timeSlot.Id,
                ClassDateTime = DateTime.UtcNow.AddHours(24),
                Status = ClassStatus.Scheduled,
                Reminder24HrSent = false,
                Reminder1HrSent = false
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SendClassReminders("test-secret-key") as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var response = result.Value;
        var remindersSentProp = response?.GetType().GetProperty("remindersSent");
        var remindersSent = remindersSentProp?.GetValue(response);
        Assert.Equal(2, remindersSent);
    }

    #endregion

    #region Health Check Tests

    [Fact]
    public void Health_ReturnsOk_WithoutAuthentication()
    {
        // Act
        var result = _controller.Health();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    #endregion

    #region Cleanup Stale Checkouts Tests

    [Fact]
    public async Task CleanupStaleCheckouts_ReturnsUnauthorized_WhenNoKeyProvided()
    {
        // Act
        var result = await _controller.CleanupStaleCheckouts(null);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CleanupStaleCheckouts_ReturnsOk_WithValidKey()
    {
        // Arrange
        _classSchedulingServiceMock.Setup(s => s.CleanupStalePendingClassesAsync(30))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CleanupStaleCheckouts("test-secret-key");

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    #endregion
}
