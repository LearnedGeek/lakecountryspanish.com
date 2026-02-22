using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;
using System.Security.Claims;
using System.Text;

namespace LakeCountrySpanish.Tests.Controllers;

public class PaymentControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IPaymentService> _paymentServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly PaymentController _controller;
    private readonly ApplicationUser _testStudent;

    public PaymentControllerTests()
    {
        _context = TestDbContextFactory.Create();

        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _paymentServiceMock = new Mock<IPaymentService>();
        _configurationMock = new Mock<IConfiguration>();

        _controller = new PaymentController(
            _context,
            _userManagerMock.Object,
            _paymentServiceMock.Object,
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

    #region Checkout GET Tests

    [Fact]
    public async Task Checkout_ReturnsNotFound_WhenUserNotAuthenticated()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Checkout(packageId: 1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Checkout_RedirectsToPurchaseClasses_WhenNoPackageId()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var result = await _controller.Checkout(packageId: null);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("PurchaseClasses", redirectResult.ActionName);
        Assert.Equal("Student", redirectResult.ControllerName);
    }

    [Fact]
    public async Task Checkout_ReturnsNotFound_WhenPackageDoesNotExist()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var result = await _controller.Checkout(packageId: 999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Checkout_ReturnsNotFound_WhenPackageInactive()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var package = new Package { Name = "Test", ClassCount = 4, Price = 100m, IsActive = false };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        var result = await _controller.Checkout(packageId: package.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Checkout_ReturnsView_WhenPackageValid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);
        _paymentServiceMock.Setup(p => p.GetClassPriceForStudentAsync(_testStudent.Id))
            .ReturnsAsync(30m);
        _configurationMock.Setup(c => c["Stripe:PublishableKey"]).Returns("pk_test_123");

        var package = new Package { Name = "4-Pack", ClassCount = 4, Price = 100m, IsActive = true };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        var result = await _controller.Checkout(packageId: package.Id);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult.Model);
    }

    #endregion

    #region CreateCheckoutSession POST Tests

    [Fact]
    public async Task CreateCheckoutSession_ReturnsNotFound_WhenUserNull()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.CreateCheckoutSession(1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateCheckoutSession_ReturnsNotFound_WhenPackageInvalid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var result = await _controller.CreateCheckoutSession(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateCheckoutSession_RedirectsToStripe_WhenValid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var package = new Package { Name = "Test", ClassCount = 4, Price = 100m, IsActive = true };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        _paymentServiceMock.Setup(p => p.CreateCheckoutSessionAsync(
                _testStudent.Id, package.Id, null, package.Price, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://checkout.stripe.com/session123");

        var result = await _controller.CreateCheckoutSession(package.Id);

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("stripe.com", redirectResult.Url);
    }

    [Fact]
    public async Task CreateCheckoutSession_RedirectsWithError_WhenServiceThrows()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testStudent);

        var package = new Package { Name = "Test", ClassCount = 4, Price = 100m, IsActive = true };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        _paymentServiceMock.Setup(p => p.CreateCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Stripe error"));

        var result = await _controller.CreateCheckoutSession(package.Id);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Checkout", redirectResult.ActionName);
        Assert.Contains("ErrorMessage", _controller.TempData.Keys);
    }

    #endregion

    #region Success GET Tests

    [Fact]
    public async Task Success_RedirectsToDashboard_WhenNoSessionId()
    {
        var result = await _controller.Success(session_id: null!);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Equal("Student", redirectResult.ControllerName);
    }

    [Fact]
    public async Task Success_SetsSuccessMessage_WhenPaymentCompleted()
    {
        var payment = new Payment
        {
            Id = 1,
            StudentId = _testStudent.Id,
            Amount = 100m,
            Status = PaymentStatusType.Completed,
            StripeSessionId = "sess_123"
        };

        _paymentServiceMock.Setup(p => p.GetPaymentBySessionIdAsync("sess_123"))
            .ReturnsAsync(payment);

        var result = await _controller.Success("sess_123");

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Contains("successful", _controller.TempData["SuccessMessage"]?.ToString());
    }

    [Fact]
    public async Task Success_SetsProcessingMessage_WhenPaymentNotCompleted()
    {
        _paymentServiceMock.Setup(p => p.GetPaymentBySessionIdAsync("sess_123"))
            .ReturnsAsync((Payment?)null);

        var result = await _controller.Success("sess_123");

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("shortly", _controller.TempData["SuccessMessage"]?.ToString());
    }

    #endregion

    #region Cancel GET Tests

    [Fact]
    public void Cancel_RedirectsToDashboard_WithErrorMessage()
    {
        var result = _controller.Cancel();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Contains("cancelled", _controller.TempData["ErrorMessage"]?.ToString());
    }

    #endregion

    #region Webhook POST Tests

    [Fact]
    public async Task Webhook_ReturnsBadRequest_WhenSignatureMissing()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        // No Stripe-Signature header

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var result = await _controller.Webhook();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Missing", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Webhook_ReturnsBadRequest_WhenSignatureInvalid()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        httpContext.Request.Headers["Stripe-Signature"] = "invalid_sig";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _paymentServiceMock.Setup(p => p.ProcessWebhookAsync(It.IsAny<string>(), "invalid_sig"))
            .ReturnsAsync(WebhookProcessingResult.SignatureInvalid());

        var result = await _controller.Webhook();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Webhook_ReturnsOk_WhenProcessingSucceeds()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"type\":\"payment_intent.succeeded\"}"));
        httpContext.Request.Headers["Stripe-Signature"] = "valid_sig";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _paymentServiceMock.Setup(p => p.ProcessWebhookAsync(It.IsAny<string>(), "valid_sig"))
            .ReturnsAsync(WebhookProcessingResult.Succeeded());

        var result = await _controller.Webhook();

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Webhook_ReturnsOk_WhenAlreadyProcessed()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        httpContext.Request.Headers["Stripe-Signature"] = "valid_sig";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _paymentServiceMock.Setup(p => p.ProcessWebhookAsync(It.IsAny<string>(), "valid_sig"))
            .ReturnsAsync(WebhookProcessingResult.Duplicate());

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

        _paymentServiceMock.Setup(p => p.ProcessWebhookAsync(It.IsAny<string>(), "valid_sig"))
            .ReturnsAsync(WebhookProcessingResult.Failed("Processing error"));

        var result = await _controller.Webhook();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region BuyPackage Tests

    [Fact]
    public async Task BuyPackage_ReturnsNotFound_WhenPackageDoesNotExist()
    {
        var result = await _controller.BuyPackage(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task BuyPackage_ReturnsNotFound_WhenPackageInactive()
    {
        var package = new Package { Name = "Test", ClassCount = 4, Price = 100m, IsActive = false };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        var result = await _controller.BuyPackage(package.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task BuyPackage_RedirectsToCheckout_WhenPackageValid()
    {
        var package = new Package { Name = "Test", ClassCount = 4, Price = 100m, IsActive = true };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        var result = await _controller.BuyPackage(package.Id);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Checkout", redirectResult.ActionName);
        Assert.Equal(package.Id, redirectResult.RouteValues?["packageId"]);
    }

    #endregion
}
