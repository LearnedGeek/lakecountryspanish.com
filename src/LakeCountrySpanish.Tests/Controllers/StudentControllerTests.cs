using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;
using System.Security.Claims;

namespace LakeCountrySpanish.Tests.Controllers;

/// <summary>
/// Tests for StudentController actions, particularly the deprecated booking flow.
/// </summary>
public class StudentControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IScheduleService> _scheduleServiceMock;
    private readonly Mock<IClassSchedulingService> _classSchedulingServiceMock;
    private readonly Mock<IPaymentService> _paymentServiceMock;
    private readonly Mock<IGamificationService> _gamificationServiceMock;
    private readonly Mock<IPlacementTestService> _placementTestServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ITicketService> _ticketServiceMock;
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly StudentController _controller;
    private readonly ApplicationUser _testStudent;

    public StudentControllerTests()
    {
        _context = TestDbContextFactory.Create();

        // Setup UserManager mock
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _scheduleServiceMock = new Mock<IScheduleService>();
        _classSchedulingServiceMock = new Mock<IClassSchedulingService>();
        _paymentServiceMock = new Mock<IPaymentService>();
        _gamificationServiceMock = new Mock<IGamificationService>();
        _placementTestServiceMock = new Mock<IPlacementTestService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _ticketServiceMock = new Mock<ITicketService>();
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
        _emailServiceMock = new Mock<IEmailService>();

        _controller = new StudentController(
            _context,
            _userManagerMock.Object,
            _scheduleServiceMock.Object,
            _classSchedulingServiceMock.Object,
            _paymentServiceMock.Object,
            _gamificationServiceMock.Object,
            _placementTestServiceMock.Object,
            _tokenServiceMock.Object,
            _ticketServiceMock.Object,
            _subscriptionServiceMock.Object,
            _emailServiceMock.Object);

        // Setup test student
        _testStudent = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "teststudent@test.com",
            Email = "teststudent@test.com",
            FirstName = "Test",
            LastName = "Student"
        };
        _context.Users.Add(_testStudent);
        _context.SaveChanges();

        // Setup HttpContext with TempData
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.TempData = tempData;

        // Setup controller context with user
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _testStudent.Id) };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region BookClass GET Tests

    [Fact]
    public void BookClass_Get_RedirectsToMyClasses()
    {
        // Act
        var result = _controller.BookClass(weekStart: null);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyClasses", redirectResult.ActionName);
    }

    [Fact]
    public void BookClass_Get_PreservesWeekStartParameter()
    {
        // Arrange
        var weekStart = new DateTime(2025, 1, 13);

        // Act
        var result = _controller.BookClass(weekStart);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyClasses", redirectResult.ActionName);
        Assert.NotNull(redirectResult.RouteValues);
        Assert.Equal(weekStart, redirectResult.RouteValues["weekStart"]);
    }

    #endregion

    #region BookClass POST Tests (Deprecated)

    [Fact]
    public void BookClass_Post_RedirectsToMyClasses()
    {
        // Arrange
        var timeSlotId = 1;
        var classDateTime = DateTime.Now.AddDays(7);

        // Act
#pragma warning disable CS0618 // Obsolete warning expected
        var result = _controller.BookClass(timeSlotId, classDateTime);
#pragma warning restore CS0618

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyClasses", redirectResult.ActionName);
    }

    [Fact]
    public void BookClass_Post_SetsInfoMessage()
    {
        // Arrange
        var timeSlotId = 1;
        var classDateTime = DateTime.Now.AddDays(7);

        // Act
#pragma warning disable CS0618 // Obsolete warning expected
        _controller.BookClass(timeSlotId, classDateTime);
#pragma warning restore CS0618

        // Assert
        Assert.True(_controller.TempData.ContainsKey("InfoMessage"));
        Assert.Contains("new scheduling page", _controller.TempData["InfoMessage"]?.ToString());
    }

    [Fact]
    public void BookClass_Post_DoesNotConsumeTickets()
    {
        // Arrange
        var timeSlotId = 1;
        var classDateTime = DateTime.Now.AddDays(7);

        // Act
#pragma warning disable CS0618 // Obsolete warning expected
        _controller.BookClass(timeSlotId, classDateTime);
#pragma warning restore CS0618

        // Assert - Verify ticket service was never called
        _ticketServiceMock.Verify(
            t => t.UseTicketForClassAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    #endregion

    #region BookRecurringClasses POST Tests (Deprecated)

    [Fact]
    public void BookRecurringClasses_Post_RedirectsToMyClasses()
    {
        // Arrange
        var timeSlotId = 1;
        var startDate = DateTime.Now.AddDays(7);
        var weekCount = 4;

        // Act
#pragma warning disable CS0618 // Obsolete warning expected
        var result = _controller.BookRecurringClasses(timeSlotId, startDate, weekCount);
#pragma warning restore CS0618

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyClasses", redirectResult.ActionName);
    }

    [Fact]
    public void BookRecurringClasses_Post_SetsInfoMessage()
    {
        // Arrange
        var timeSlotId = 1;
        var startDate = DateTime.Now.AddDays(7);
        var weekCount = 4;

        // Act
#pragma warning disable CS0618 // Obsolete warning expected
        _controller.BookRecurringClasses(timeSlotId, startDate, weekCount);
#pragma warning restore CS0618

        // Assert
        Assert.True(_controller.TempData.ContainsKey("InfoMessage"));
        Assert.Contains("subscription", _controller.TempData["InfoMessage"]?.ToString());
    }

    [Fact]
    public void BookRecurringClasses_Post_DoesNotBookClasses()
    {
        // Arrange
        var timeSlotId = 1;
        var startDate = DateTime.Now.AddDays(7);
        var weekCount = 4;

        // Act
#pragma warning disable CS0618 // Obsolete warning expected
        _controller.BookRecurringClasses(timeSlotId, startDate, weekCount);
#pragma warning restore CS0618

        // Assert - Verify schedule service was never called
        _scheduleServiceMock.Verify(
            s => s.BookRecurringClassesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()),
            Times.Never);
    }

    #endregion

    #region CancelClass Tests

    [Fact]
    public async Task CancelClass_ReturnsNotFound_WhenUserNotAuthenticated()
    {
        // Arrange
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _controller.CancelClass(1, false);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CancelClass_RedirectsWithError_WhenClassNotFound()
    {
        // Arrange
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        // Act
        var result = await _controller.CancelClass(999, false);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Contains("ErrorMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task CancelClass_RedirectsWithError_WhenClassBelongsToDifferentStudent()
    {
        // Arrange
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var otherStudent = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "other@test.com",
            Email = "other@test.com"
        };
        _context.Users.Add(otherStudent);

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
            StudentId = otherStudent.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(3),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.CancelClass(scheduledClass.Id, false);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Contains("ErrorMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task CancelClass_SendsAdminNotification_WhenSuccessful()
    {
        // Arrange
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

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
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(3),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        _scheduleServiceMock.Setup(s => s.GetCancellationStatusAsync(scheduledClass.Id))
            .ReturnsAsync((true, false));
        _scheduleServiceMock.Setup(s => s.CancelClassWithForfeitAsync(scheduledClass.Id, _testStudent.Id, false, false))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelClass(scheduledClass.Id, false);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendAdminClassCancelledAsync(
                It.IsAny<string>(),
                _testStudent.Email!,
                scheduledClass.ClassDateTime,
                "Cancelled by student"),
            Times.Once);
    }

    [Fact]
    public async Task CancelClass_SendsLateCancellationReason_WhenWithin24Hours()
    {
        // Arrange
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

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
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddHours(12), // Within 24 hours
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        _scheduleServiceMock.Setup(s => s.GetCancellationStatusAsync(scheduledClass.Id))
            .ReturnsAsync((true, true)); // Will forfeit credit
        _scheduleServiceMock.Setup(s => s.CancelClassWithForfeitAsync(scheduledClass.Id, _testStudent.Id, false, true))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelClass(scheduledClass.Id, true);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendAdminClassCancelledAsync(
                It.IsAny<string>(),
                _testStudent.Email!,
                scheduledClass.ClassDateTime,
                "Late cancellation (within 24 hours)"),
            Times.Once);
    }

    [Fact]
    public async Task CancelClass_DoesNotSendAdminNotification_WhenCancellationFails()
    {
        // Arrange
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

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
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(3),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        _scheduleServiceMock.Setup(s => s.GetCancellationStatusAsync(scheduledClass.Id))
            .ReturnsAsync((true, false));
        _scheduleServiceMock.Setup(s => s.CancelClassWithForfeitAsync(scheduledClass.Id, _testStudent.Id, false, false))
            .ReturnsAsync(false); // Cancellation fails

        // Act
        var result = await _controller.CancelClass(scheduledClass.Id, false);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendAdminClassCancelledAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>()),
            Times.Never);
    }

    #endregion
}
