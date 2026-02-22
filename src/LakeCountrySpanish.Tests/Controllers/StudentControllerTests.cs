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
        var controllerHttpContext = new DefaultHttpContext { User = claimsPrincipal };
        controllerHttpContext.Request.Scheme = "https";
        controllerHttpContext.Request.Host = new HostString("localhost");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = controllerHttpContext
        };

        // Setup URL helper for actions that use Url.Action()
        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock.Setup(u => u.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()))
            .Returns("https://localhost/test");
        _controller.Url = urlHelperMock.Object;
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

    #region AddToCart Tests

    [Fact]
    public async Task AddToCart_ReturnsUnauthorized_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.AddToCart(1, DateTime.UtcNow.AddDays(3));

        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousProperty(jsonResult.Value!, "success");
        Assert.Equal(false, data);
    }

    [Fact]
    public async Task AddToCart_RejectsInvalidTimeSlot()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var result = await _controller.AddToCart(999, DateTime.UtcNow.AddDays(3));

        var jsonResult = Assert.IsType<JsonResult>(result);
        var error = GetAnonymousProperty(jsonResult.Value!, "error");
        Assert.Contains("Invalid", error?.ToString());
    }

    [Fact]
    public async Task AddToCart_RejectsClassWithin24Hours()
    {
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

        // Class within 24 hours
        var result = await _controller.AddToCart(timeSlot.Id, DateTime.UtcNow.AddHours(12));

        var jsonResult = Assert.IsType<JsonResult>(result);
        var error = GetAnonymousProperty(jsonResult.Value!, "error");
        Assert.Contains("24 hours", error?.ToString());
    }

    [Fact]
    public async Task AddToCart_AddsToCart_ForNonSubscriber()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionAsync(_testStudent.Id))
            .ReturnsAsync((Subscription?)null);

        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var classDateTime = DateTime.UtcNow.AddDays(3);
        var pendingClass = new ScheduledClass
        {
            Id = 100,
            StudentId = _testStudent.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = classDateTime,
            IsPendingCheckout = true
        };
        _classSchedulingServiceMock.Setup(c => c.AddClassToCheckoutAsync(
                _testStudent.Id, timeSlot.Id, classDateTime))
            .ReturnsAsync(pendingClass);
        _classSchedulingServiceMock.Setup(c => c.GetCheckoutSummaryAsync(_testStudent.Id, 0))
            .ReturnsAsync(new LakeCountrySpanish.Web.Models.ViewModels.SchedulingCheckoutSummary
            {
                ClassCount = 1,
                PerClassPrice = 30m,
                Subtotal = 30m,
                TotalDue = 30m
            });

        var result = await _controller.AddToCart(timeSlot.Id, classDateTime);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var success = GetAnonymousProperty(jsonResult.Value!, "success");
        Assert.Equal(true, success);
        var confirmed = GetAnonymousProperty(jsonResult.Value!, "confirmed");
        Assert.Equal(false, confirmed);
    }

    #endregion

    #region RemoveFromCart Tests

    [Fact]
    public async Task RemoveFromCart_ReturnsUnauthorized_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.RemoveFromCart(1);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = GetAnonymousProperty(jsonResult.Value!, "success");
        Assert.Equal(false, data);
    }

    [Fact]
    public async Task RemoveFromCart_ReturnsError_WhenRemoveFails()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _classSchedulingServiceMock.Setup(c => c.RemoveFromCheckoutAsync(1, _testStudent.Id))
            .ReturnsAsync(false);

        var result = await _controller.RemoveFromCart(1);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var error = GetAnonymousProperty(jsonResult.Value!, "error");
        Assert.NotNull(error);
    }

    [Fact]
    public async Task RemoveFromCart_ReturnsSuccess_WhenRemoved()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _classSchedulingServiceMock.Setup(c => c.RemoveFromCheckoutAsync(1, _testStudent.Id))
            .ReturnsAsync(true);
        _classSchedulingServiceMock.Setup(c => c.GetCheckoutSummaryAsync(_testStudent.Id, 0))
            .ReturnsAsync(new LakeCountrySpanish.Web.Models.ViewModels.SchedulingCheckoutSummary
            {
                ClassCount = 0,
                PerClassPrice = 30m,
                Subtotal = 0m,
                TotalDue = 0m
            });

        var result = await _controller.RemoveFromCart(1);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var success = GetAnonymousProperty(jsonResult.Value!, "success");
        Assert.Equal(true, success);
    }

    #endregion

    #region ClearCart Tests

    [Fact]
    public async Task ClearCart_ReturnsUnauthorized_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.ClearCart();

        var jsonResult = Assert.IsType<JsonResult>(result);
        var success = GetAnonymousProperty(jsonResult.Value!, "success");
        Assert.Equal(false, success);
    }

    [Fact]
    public async Task ClearCart_ReturnsSuccess_WithClearedCount()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _classSchedulingServiceMock.Setup(c => c.ClearPendingCheckoutAsync(_testStudent.Id))
            .ReturnsAsync(3);

        var result = await _controller.ClearCart();

        var jsonResult = Assert.IsType<JsonResult>(result);
        var success = GetAnonymousProperty(jsonResult.Value!, "success");
        Assert.Equal(true, success);
    }

    #endregion

    #region GetCheckoutSummary Tests

    [Fact]
    public async Task GetCheckoutSummary_ReturnsUnauthorized_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetCheckoutSummary();

        var jsonResult = Assert.IsType<JsonResult>(result);
        var error = GetAnonymousProperty(jsonResult.Value!, "error");
        Assert.NotNull(error);
    }

    [Fact]
    public async Task GetCheckoutSummary_ReturnsSummary_WhenValid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _classSchedulingServiceMock.Setup(c => c.GetCheckoutSummaryAsync(_testStudent.Id, 0))
            .ReturnsAsync(new LakeCountrySpanish.Web.Models.ViewModels.SchedulingCheckoutSummary
            {
                ClassCount = 2,
                PerClassPrice = 30m,
                Subtotal = 60m,
                TotalDue = 60m
            });
        _ticketServiceMock.Setup(t => t.GetAvailableTicketCountAsync(_testStudent.Id))
            .ReturnsAsync(1);

        var result = await _controller.GetCheckoutSummary();

        var jsonResult = Assert.IsType<JsonResult>(result);
        var classCount = GetAnonymousProperty(jsonResult.Value!, "classCount");
        Assert.Equal(2, classCount);
    }

    #endregion

    #region GetPendingCart Tests

    [Fact]
    public async Task GetPendingCart_ReturnsUnauthorized_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetPendingCart();

        var jsonResult = Assert.IsType<JsonResult>(result);
        var error = GetAnonymousProperty(jsonResult.Value!, "error");
        Assert.NotNull(error);
    }

    [Fact]
    public async Task GetPendingCart_ReturnsEmptyArray_WhenNoPendingClasses()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _classSchedulingServiceMock.Setup(c => c.GetPendingCheckoutAsync(_testStudent.Id))
            .ReturnsAsync(new List<ScheduledClass>());

        var result = await _controller.GetPendingCart();

        Assert.IsType<JsonResult>(result);
    }

    #endregion

    #region Checkout POST Tests

    [Fact]
    public async Task Checkout_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Checkout(0);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Checkout_RedirectsWithError_WhenNegativeTickets()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var result = await _controller.Checkout(-1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyClasses", redirectResult.ActionName);
        Assert.Contains("ErrorMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Checkout_RedirectsWithError_WhenCheckoutSessionFails()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _classSchedulingServiceMock.Setup(c => c.CreateCheckoutSessionAsync(
                _testStudent.Id, 0, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var result = await _controller.Checkout(0);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyClasses", redirectResult.ActionName);
        Assert.Contains("ErrorMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Checkout_RedirectsToMyClasses_WhenFreeCheckout()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _classSchedulingServiceMock.Setup(c => c.CreateCheckoutSessionAsync(
                _testStudent.Id, 2, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://localhost/Student/MyClasses?free=true");

        var result = await _controller.Checkout(2);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyClasses", redirectResult.ActionName);
        Assert.Contains("SuccessMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Checkout_RedirectsToStripe_WhenPaidCheckout()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _classSchedulingServiceMock.Setup(c => c.CreateCheckoutSessionAsync(
                _testStudent.Id, 0, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://checkout.stripe.com/sess_123");

        var result = await _controller.Checkout(0);

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("stripe.com", redirectResult.Url);
    }

    #endregion

    #region CheckoutSuccess Tests

    [Fact]
    public async Task CheckoutSuccess_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.CheckoutSuccess(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CheckoutSuccess_RedirectsToMyClasses_WithSuccessMessage()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var result = await _controller.CheckoutSuccess(Guid.NewGuid());

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyClasses", redirectResult.ActionName);
        Assert.Contains("SuccessMessage", _controller.TempData.Keys);
    }

    #endregion

    #region Helper Methods

    private static object? GetAnonymousProperty(object obj, string propertyName)
    {
        return obj.GetType().GetProperty(propertyName)?.GetValue(obj);
    }

    #endregion
}
