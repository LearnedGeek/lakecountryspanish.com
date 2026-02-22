using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Security.Claims;

namespace LakeCountrySpanish.Tests.Controllers;

public class AssignmentControllerTests : IDisposable
{
    private readonly Mock<IAssignmentService> _assignmentServiceMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<AssignmentController>> _loggerMock;
    private readonly AssignmentController _controller;
    private readonly ApplicationUser _testStudent;

    public AssignmentControllerTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _assignmentServiceMock = new Mock<IAssignmentService>();
        _loggerMock = new Mock<ILogger<AssignmentController>>();

        _controller = new AssignmentController(
            _assignmentServiceMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object);

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

        var result = await _controller.Index(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_ReturnsView_WithAssignments()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _assignmentServiceMock.Setup(s => s.GetStudentAssignmentsAsync(It.IsAny<string>(), It.IsAny<StudentAssignmentStatus?>()))
            .ReturnsAsync(new List<StudentAssignment>());
        _assignmentServiceMock.Setup(s => s.GetStudentStatsAsync(_testStudent.Id))
            .ReturnsAsync(new StudentAssignmentStats());
        _assignmentServiceMock.Setup(s => s.GetStudentSubmissionsAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<AssignmentSubmission>());

        var result = await _controller.Index(null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<StudentAssignmentListViewModel>(viewResult.Model);
    }

    #endregion

    #region Take Tests

    [Fact]
    public async Task Take_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Take(1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Take_ReturnsNotFound_WhenAssignmentNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _assignmentServiceMock.Setup(s => s.GetStudentAssignmentAsync(1))
            .ReturnsAsync((StudentAssignment?)null);

        var result = await _controller.Take(1);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Submit Tests

    [Fact]
    public async Task Submit_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var model = new SubmitAssignmentViewModel { AssignmentId = 1 };
        var result = await _controller.Submit(model);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Skip Tests

    [Fact]
    public async Task Skip_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Skip(1);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region History Tests

    [Fact]
    public async Task History_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.History();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task History_ReturnsView_WithSubmissions()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _assignmentServiceMock.Setup(s => s.GetStudentSubmissionsAsync(_testStudent.Id, 50))
            .ReturnsAsync(new List<AssignmentSubmission>());

        var result = await _controller.History();

        Assert.IsType<ViewResult>(result);
    }

    #endregion
}
