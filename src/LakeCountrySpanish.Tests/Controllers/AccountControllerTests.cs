using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using System.Security.Claims;

namespace LakeCountrySpanish.Tests.Controllers;

public class AccountControllerTests : IDisposable
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly AccountController _controller;

    public AccountControllerTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object, contextAccessor.Object, claimsFactory.Object, null!, null!, null!, null!);

        _controller = new AccountController(_signInManagerMock.Object, _userManagerMock.Object);

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.TempData = tempData;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose() { }

    #region Login GET Tests

    [Fact]
    public void Login_Get_ReturnsView()
    {
        var result = _controller.Login(returnUrl: null);

        Assert.IsType<ViewResult>(result);
    }

    #endregion

    #region Login POST Tests

    [Fact]
    public async Task Login_Post_ReturnsView_WhenModelInvalid()
    {
        _controller.ModelState.AddModelError("Email", "Required");

        var result = await _controller.Login(new LoginViewModel());

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Login_Post_ReturnsView_WhenUserNotFound()
    {
        _userManagerMock.Setup(u => u.FindByEmailAsync("unknown@test.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var model = new LoginViewModel { Email = "unknown@test.com", Password = "pass" };
        var result = await _controller.Login(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Login_Post_ReturnsView_WhenUserNotActive()
    {
        var user = new ApplicationUser
        {
            Id = "1",
            Email = "inactive@test.com",
            UserName = "inactive@test.com",
            IsActive = false
        };
        _userManagerMock.Setup(u => u.FindByEmailAsync("inactive@test.com"))
            .ReturnsAsync(user);

        var model = new LoginViewModel { Email = "inactive@test.com", Password = "pass" };
        var result = await _controller.Login(model);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Login_Post_RedirectsToAdminDashboard_WhenAdminLogin()
    {
        var user = new ApplicationUser
        {
            Id = "1",
            Email = "admin@test.com",
            UserName = "admin@test.com",
            IsActive = true,
            MustChangePassword = false
        };
        _userManagerMock.Setup(u => u.FindByEmailAsync("admin@test.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.PasswordSignInAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManagerMock.Setup(u => u.IsInRoleAsync(user, "Admin"))
            .ReturnsAsync(true);

        // Setup Url helper for ReturnUrl check
        var urlHelperMock = new Mock<IUrlHelper>();
        _controller.Url = urlHelperMock.Object;

        var model = new LoginViewModel { Email = "admin@test.com", Password = "pass" };
        var result = await _controller.Login(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Equal("Admin", redirectResult.ControllerName);
    }

    [Fact]
    public async Task Login_Post_RedirectsToStudentDashboard_WhenStudentLogin()
    {
        var user = new ApplicationUser
        {
            Id = "1",
            Email = "student@test.com",
            UserName = "student@test.com",
            IsActive = true,
            MustChangePassword = false
        };
        _userManagerMock.Setup(u => u.FindByEmailAsync("student@test.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.PasswordSignInAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManagerMock.Setup(u => u.IsInRoleAsync(user, "Admin"))
            .ReturnsAsync(false);

        var urlHelperMock = new Mock<IUrlHelper>();
        _controller.Url = urlHelperMock.Object;

        var model = new LoginViewModel { Email = "student@test.com", Password = "pass" };
        var result = await _controller.Login(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Equal("Student", redirectResult.ControllerName);
    }

    [Fact]
    public async Task Login_Post_RedirectsToChangePassword_WhenMustChange()
    {
        var user = new ApplicationUser
        {
            Id = "1",
            Email = "newuser@test.com",
            UserName = "newuser@test.com",
            IsActive = true,
            MustChangePassword = true
        };
        _userManagerMock.Setup(u => u.FindByEmailAsync("newuser@test.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.PasswordSignInAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var model = new LoginViewModel { Email = "newuser@test.com", Password = "pass" };
        var result = await _controller.Login(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ChangePassword", redirectResult.ActionName);
    }

    [Fact]
    public async Task Login_Post_ReturnsView_WhenPasswordIncorrect()
    {
        var user = new ApplicationUser
        {
            Id = "1",
            Email = "test@test.com",
            UserName = "test@test.com",
            IsActive = true
        };
        _userManagerMock.Setup(u => u.FindByEmailAsync("test@test.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.PasswordSignInAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var model = new LoginViewModel { Email = "test@test.com", Password = "wrong" };
        var result = await _controller.Login(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_SignsOut_WhenAuthenticated()
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        var identity = new ClaimsIdentity(claims, "Test");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        var result = await _controller.Logout();

        _signInManagerMock.Verify(s => s.SignOutAsync(), Times.Once);
    }

    #endregion

    #region LogoutPost Tests

    [Fact]
    public async Task LogoutPost_SignsOut_RedirectsToHome()
    {
        var result = await _controller.LogoutPost();

        _signInManagerMock.Verify(s => s.SignOutAsync(), Times.Once);
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal("Home", redirectResult.ControllerName);
    }

    #endregion

    #region AccessDenied Tests

    [Fact]
    public void AccessDenied_ReturnsView()
    {
        var result = _controller.AccessDenied();

        Assert.IsType<ViewResult>(result);
    }

    #endregion

    #region ChangePassword Tests

    [Fact]
    public async Task ChangePassword_Post_ReturnsView_WhenModelInvalid()
    {
        _controller.ModelState.AddModelError("CurrentPassword", "Required");
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = "1", Email = "t@t.com" });

        var result = await _controller.ChangePassword(new ChangePasswordViewModel());

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ChangePassword_Post_RedirectsBasedOnRole_WhenSuccessful()
    {
        var user = new ApplicationUser { Id = "1", Email = "t@t.com", UserName = "t@t.com", MustChangePassword = true };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(u => u.ChangePasswordAsync(user, "old", "new"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.IsInRoleAsync(user, "Admin"))
            .ReturnsAsync(false);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        var identity = new ClaimsIdentity(claims, "Test");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "old",
            NewPassword = "new",
            ConfirmPassword = "new"
        };
        var result = await _controller.ChangePassword(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Equal("Student", redirectResult.ControllerName);
    }

    #endregion
}
