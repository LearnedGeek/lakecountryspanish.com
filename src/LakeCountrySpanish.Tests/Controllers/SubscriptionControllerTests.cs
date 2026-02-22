using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Security.Claims;
using System.Text;

namespace LakeCountrySpanish.Tests.Controllers;

public class SubscriptionControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly SubscriptionController _controller;
    private readonly ApplicationUser _testStudent;

    public SubscriptionControllerTests()
    {
        _context = TestDbContextFactory.Create();

        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _subscriptionServiceMock = new Mock<ISubscriptionService>();
        _configurationMock = new Mock<IConfiguration>();

        _controller = new SubscriptionController(
            _context,
            _userManagerMock.Object,
            _subscriptionServiceMock.Object,
            _configurationMock.Object);

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

        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.TempData = tempData;

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _testStudent.Id) };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        httpContext.User = claimsPrincipal;

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock.Setup(u => u.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()))
            .Returns("https://localhost/test");
        _controller.Url = urlHelperMock.Object;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Plans Tests

    [Fact]
    public async Task Plans_ReturnsView_WithTiers()
    {
        var tiers = new List<SubscriptionTier>
        {
            new() { Id = 1, Name = "Basic", ClassesPerMonth = 4, MonthlyPrice = 100m, IsActive = true }
        };
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionTiersAsync())
            .ReturnsAsync(tiers);
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionAsync(_testStudent.Id))
            .ReturnsAsync((Subscription?)null);

        var result = await _controller.Plans();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SubscriptionPlansViewModel>(viewResult.Model);
        Assert.Single(model.Tiers);
    }

    [Fact]
    public async Task Plans_IncludesCurrentSubscription_WhenAuthenticated()
    {
        var subscription = new Subscription
        {
            Id = 1,
            StudentId = _testStudent.Id,
            Status = SubscriptionStatus.Active
        };
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionTiersAsync())
            .ReturnsAsync(new List<SubscriptionTier>());
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionAsync(_testStudent.Id))
            .ReturnsAsync(subscription);

        var result = await _controller.Plans();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SubscriptionPlansViewModel>(viewResult.Model);
        Assert.NotNull(model.CurrentSubscription);
    }

    #endregion

    #region Subscribe POST Tests

    [Fact]
    public async Task Subscribe_RedirectsToLogin_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Subscribe(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirectResult.ActionName);
        Assert.Equal("Account", redirectResult.ControllerName);
    }

    [Fact]
    public async Task Subscribe_RedirectsToManage_WhenAlreadySubscribed()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionAsync(_testStudent.Id))
            .ReturnsAsync(new Subscription { Id = 1, StudentId = _testStudent.Id });

        var result = await _controller.Subscribe(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Subscribe_RedirectsToStripe_WhenValid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionAsync(_testStudent.Id))
            .ReturnsAsync((Subscription?)null);
        _subscriptionServiceMock.Setup(s => s.CreateSubscriptionCheckoutAsync(
                _testStudent.Id, 1, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://checkout.stripe.com/sub_123");

        var result = await _controller.Subscribe(1);

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("stripe.com", redirectResult.Url);
    }

    [Fact]
    public async Task Subscribe_RedirectsToPlans_WhenServiceThrows()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionAsync(_testStudent.Id))
            .ReturnsAsync((Subscription?)null);
        _subscriptionServiceMock.Setup(s => s.CreateSubscriptionCheckoutAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Stripe error"));

        var result = await _controller.Subscribe(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Plans", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    #endregion

    #region Success Tests

    [Fact]
    public async Task Success_RedirectsToLogin_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Success("sess_123");

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirectResult.ActionName);
    }

    [Fact]
    public async Task Success_RedirectsToManage_WithSuccessMessage()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var result = await _controller.Success("sess_123");

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Success", _controller.TempData.Keys);
    }

    #endregion

    #region Manage Tests

    [Fact]
    public async Task Manage_RedirectsToLogin_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Manage();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirectResult.ActionName);
    }

    [Fact]
    public async Task Manage_ReturnsView_WithNoSubscription()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionAsync(_testStudent.Id))
            .ReturnsAsync((Subscription?)null);
        _subscriptionServiceMock.Setup(s => s.GetStudentSubscriptionsAsync(_testStudent.Id))
            .ReturnsAsync(new List<Subscription>());

        var result = await _controller.Manage();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SubscriptionManageViewModel>(viewResult.Model);
        Assert.Null(model.CurrentSubscription);
    }

    [Fact]
    public async Task Manage_ReturnsView_WithActiveSubscription()
    {
        var subscription = new Subscription
        {
            Id = 1,
            StudentId = _testStudent.Id,
            Status = SubscriptionStatus.Active
        };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetActiveSubscriptionAsync(_testStudent.Id))
            .ReturnsAsync(subscription);
        _subscriptionServiceMock.Setup(s => s.GetStudentSubscriptionsAsync(_testStudent.Id))
            .ReturnsAsync(new List<Subscription> { subscription });
        _subscriptionServiceMock.Setup(s => s.GetRecurringSchedulesAsync(subscription.Id))
            .ReturnsAsync(new List<RecurringSchedule>());
        _subscriptionServiceMock.Setup(s => s.CanCancelThisMonth(subscription))
            .Returns(true);
        _subscriptionServiceMock.Setup(s => s.GetEffectiveCancellationDate(subscription))
            .Returns(DateTime.Today.AddMonths(1));

        var result = await _controller.Manage();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SubscriptionManageViewModel>(viewResult.Model);
        Assert.NotNull(model.CurrentSubscription);
    }

    #endregion

    #region SetSchedule POST Tests

    [Fact]
    public async Task SetSchedule_RedirectsToLogin_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.SetSchedule(1, new List<RecurringScheduleInput>());

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirectResult.ActionName);
    }

    [Fact]
    public async Task SetSchedule_RedirectsWithError_WhenSubscriptionNotFound()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetSubscriptionAsync(999))
            .ReturnsAsync((Subscription?)null);

        var result = await _controller.SetSchedule(999, new List<RecurringScheduleInput>());

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    [Fact]
    public async Task SetSchedule_RedirectsWithError_WhenSubscriptionBelongsToOther()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetSubscriptionAsync(1))
            .ReturnsAsync(new Subscription { Id = 1, StudentId = "other-id" });

        var result = await _controller.SetSchedule(1, new List<RecurringScheduleInput>());

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    [Fact]
    public async Task SetSchedule_RedirectsWithSuccess_WhenScheduleUpdated()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetSubscriptionAsync(1))
            .ReturnsAsync(new Subscription { Id = 1, StudentId = _testStudent.Id });
        _subscriptionServiceMock.Setup(s => s.SetRecurringScheduleAsync(1, It.IsAny<IEnumerable<RecurringScheduleRequest>>()))
            .ReturnsAsync(true);

        var schedules = new List<RecurringScheduleInput>
        {
            new() { IsSelected = true, TimeSlotId = 1, DayOfWeek = DayOfWeek.Monday, Time = TimeSpan.FromHours(10) }
        };

        var result = await _controller.SetSchedule(1, schedules);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Success", _controller.TempData.Keys);
    }

    #endregion

    #region RequestCancellation POST Tests

    [Fact]
    public async Task RequestCancellation_RedirectsToLogin_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.RequestCancellation(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirectResult.ActionName);
    }

    [Fact]
    public async Task RequestCancellation_RedirectsWithSuccess_WhenCancelled()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.RequestCancellationAsync(1, _testStudent.Id))
            .ReturnsAsync(true);
        _subscriptionServiceMock.Setup(s => s.GetSubscriptionAsync(1))
            .ReturnsAsync(new Subscription
            {
                Id = 1,
                StudentId = _testStudent.Id,
                EffectiveCancellationDate = DateTime.Today.AddMonths(1)
            });

        var result = await _controller.RequestCancellation(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Success", _controller.TempData.Keys);
    }

    [Fact]
    public async Task RequestCancellation_RedirectsWithError_WhenFails()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.RequestCancellationAsync(1, _testStudent.Id))
            .ReturnsAsync(false);

        var result = await _controller.RequestCancellation(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    #endregion

    #region Pause POST Tests

    [Fact]
    public async Task Pause_RedirectsToLogin_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Pause(1, DateTime.Today.AddMonths(1));

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirectResult.ActionName);
    }

    [Fact]
    public async Task Pause_RedirectsWithSuccess_WhenPaused()
    {
        var resumeDate = DateTime.Today.AddMonths(1);
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.PauseSubscriptionAsync(1, _testStudent.Id, resumeDate))
            .ReturnsAsync(true);

        var result = await _controller.Pause(1, resumeDate);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Success", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Pause_RedirectsWithError_WhenFails()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.PauseSubscriptionAsync(1, _testStudent.Id, It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        var result = await _controller.Pause(1, DateTime.Today.AddMonths(1));

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    #endregion

    #region Resume POST Tests

    [Fact]
    public async Task Resume_RedirectsToLogin_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Resume(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirectResult.ActionName);
    }

    [Fact]
    public async Task Resume_RedirectsWithSuccess_WhenResumed()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.ResumeSubscriptionAsync(1, _testStudent.Id))
            .ReturnsAsync(true);

        var result = await _controller.Resume(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Success", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Resume_RedirectsWithError_WhenFails()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.ResumeSubscriptionAsync(1, _testStudent.Id))
            .ReturnsAsync(false);

        var result = await _controller.Resume(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    #endregion

    #region Portal Tests

    [Fact]
    public async Task Portal_RedirectsToLogin_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Portal();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirectResult.ActionName);
    }

    [Fact]
    public async Task Portal_RedirectsToStripePortal_WhenUrlReturned()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetCustomerPortalUrlAsync(_testStudent.Id, It.IsAny<string>()))
            .ReturnsAsync("https://billing.stripe.com/portal/123");

        var result = await _controller.Portal();

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("stripe.com", redirectResult.Url);
    }

    [Fact]
    public async Task Portal_RedirectsToManage_WhenNoPortalUrl()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _subscriptionServiceMock.Setup(s => s.GetCustomerPortalUrlAsync(_testStudent.Id, It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var result = await _controller.Portal();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manage", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    #endregion

    #region Webhook POST Tests

    [Fact]
    public async Task Webhook_ReturnsBadRequest_WhenSignatureMissing()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var result = await _controller.Webhook();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Missing", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Webhook_ReturnsOk_WhenProcessingSucceeds()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        httpContext.Request.Headers["Stripe-Signature"] = "valid_sig";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _subscriptionServiceMock.Setup(s => s.ProcessSubscriptionWebhookAsync(It.IsAny<string>(), "valid_sig"))
            .ReturnsAsync(true);

        var result = await _controller.Webhook();

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Webhook_Returns500_WhenProcessingFails()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        httpContext.Request.Headers["Stripe-Signature"] = "valid_sig";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _subscriptionServiceMock.Setup(s => s.ProcessSubscriptionWebhookAsync(It.IsAny<string>(), "valid_sig"))
            .ReturnsAsync(false);

        var result = await _controller.Webhook();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion
}
