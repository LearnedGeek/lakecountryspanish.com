using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Security.Claims;

namespace LakeCountrySpanish.Tests.Controllers;

public class PlacementTestControllerTests : IDisposable
{
    private readonly Mock<IPlacementTestService> _placementTestServiceMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly PlacementTestController _controller;
    private readonly ApplicationUser _testStudent;

    public PlacementTestControllerTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _placementTestServiceMock = new Mock<IPlacementTestService>();

        _controller = new PlacementTestController(
            _placementTestServiceMock.Object,
            _userManagerMock.Object);

        _testStudent = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "student@test.com",
            FirstName = "Test",
            LastName = "Student"
        };

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.TempData = tempData;

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _testStudent.Id) };
        var identity = new ClaimsIdentity(claims, "Test");
        httpContext.User = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose() { }

    #region Index Tests

    [Fact]
    public async Task Index_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Index();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_ReturnsView_WithTestInfo()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _placementTestServiceMock.Setup(s => s.GetActiveSessionAsync(_testStudent.Id))
            .ReturnsAsync((PlacementTestSession?)null);
        _placementTestServiceMock.Setup(s => s.GetLastCompletedTestAsync(_testStudent.Id))
            .ReturnsAsync((PlacementTestSession?)null);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<PlacementTestIndexViewModel>(viewResult.Model);
    }

    #endregion

    #region Start Tests

    [Fact]
    public async Task Start_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Start();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Start_RedirectsToQuestion_WhenTestStarted()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _placementTestServiceMock.Setup(s => s.StartTestAsync(_testStudent.Id))
            .ReturnsAsync(new PlacementTestSession { Id = 42, StudentId = _testStudent.Id });

        var result = await _controller.Start();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Question", redirectResult.ActionName);
        Assert.Equal(42, redirectResult.RouteValues?["sessionId"]);
    }

    #endregion

    #region Question Tests

    [Fact]
    public async Task Question_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Question(1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Question_ReturnsNotFound_WhenSessionNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _placementTestServiceMock.Setup(s => s.GetSessionByIdAsync(1))
            .ReturnsAsync((PlacementTestSession?)null);

        var result = await _controller.Question(1);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Abandon Tests

    [Fact]
    public async Task Abandon_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Abandon(1);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Resume Tests

    [Fact]
    public async Task Resume_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Resume();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Resume_RedirectsToIndex_WhenNoActiveSession()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _placementTestServiceMock.Setup(s => s.GetActiveSessionAsync(_testStudent.Id))
            .ReturnsAsync((PlacementTestSession?)null);

        var result = await _controller.Resume();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
    }

    [Fact]
    public async Task Resume_RedirectsToQuestion_WhenActiveSessionExists()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _placementTestServiceMock.Setup(s => s.GetActiveSessionAsync(_testStudent.Id))
            .ReturnsAsync(new PlacementTestSession { Id = 42, StudentId = _testStudent.Id });

        var result = await _controller.Resume();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Question", redirectResult.ActionName);
        Assert.Equal(42, redirectResult.RouteValues?["sessionId"]);
    }

    #endregion
}
