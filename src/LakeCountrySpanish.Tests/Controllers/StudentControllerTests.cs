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
            _subscriptionServiceMock.Object);

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
}
