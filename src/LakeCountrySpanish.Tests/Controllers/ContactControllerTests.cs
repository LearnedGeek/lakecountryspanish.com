using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Net;

namespace LakeCountrySpanish.Tests.Controllers;

public class ContactControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<ContactController>> _loggerMock;
    private readonly ContactController _controller;

    public ContactControllerTests()
    {
        _context = TestDbContextFactory.Create();
        _configurationMock = new Mock<IConfiguration>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<ContactController>>();

        // Setup reCAPTCHA config
        _configurationMock.Setup(c => c["Recaptcha:SiteKey"]).Returns("test-site-key");
        _configurationMock.Setup(c => c["Recaptcha:SecretKey"]).Returns("test-secret-key");

        // Setup mock HTTP client for reCAPTCHA
        SetupRecaptchaMock(true, 0.9);

        _controller = new ContactController(
            _context,
            _configurationMock.Object,
            _httpClientFactoryMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.TempData = tempData;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private void SetupRecaptchaMock(bool success, double score)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"{{\"success\": {success.ToString().ToLower()}, \"score\": {score}}}")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Index GET Tests

    [Fact]
    public void Index_Get_ReturnsView()
    {
        var result = _controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    #endregion

    #region Index POST Tests

    [Fact]
    public async Task Index_Post_ReturnsView_WhenModelInvalid()
    {
        _controller.ModelState.AddModelError("Name", "Required");

        var model = new ContactViewModel();
        var result = await _controller.Index(model);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Index_Post_RejectsSubmission_WhenHoneypotFilled()
    {
        var model = new ContactViewModel
        {
            Name = "Test",
            Email = "test@test.com",
            Message = "Hello",
            Website = "http://spam.com", // Honeypot field
            RecaptchaToken = "token"
        };

        var result = await _controller.Index(model);

        // Should redirect to ThankYou silently (bot detection)
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ThankYou", redirectResult.ActionName);
        // But should NOT save to database
        Assert.Equal(0, _context.ContactInquiries.Count());
    }

    [Fact]
    public async Task Index_Post_SavesInquiryToDatabase()
    {
        var model = new ContactViewModel
        {
            Name = "Test User",
            Email = "test@test.com",
            Phone = "555-1234",
            Message = "I'm interested in classes",
            RecaptchaToken = "valid-token"
        };

        var result = await _controller.Index(model);

        Assert.Equal(1, _context.ContactInquiries.Count());
        var inquiry = _context.ContactInquiries.First();
        Assert.Equal("Test User", inquiry.Name);
        Assert.Equal("test@test.com", inquiry.Email);
    }

    [Fact]
    public async Task Index_Post_SendsAdminNotificationEmail()
    {
        var model = new ContactViewModel
        {
            Name = "Test User",
            Email = "test@test.com",
            Message = "I want to learn Spanish",
            RecaptchaToken = "valid-token"
        };

        var result = await _controller.Index(model);

        _emailServiceMock.Verify(
            e => e.SendAdminContactInquiryAsync(
                "Test User", "test@test.com", It.IsAny<string>(), "I want to learn Spanish"),
            Times.Once);
    }

    [Fact]
    public async Task Index_Post_RedirectsToThankYou_OnSuccess()
    {
        var model = new ContactViewModel
        {
            Name = "Test",
            Email = "test@test.com",
            Message = "Hello",
            RecaptchaToken = "valid-token"
        };

        var result = await _controller.Index(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ThankYou", redirectResult.ActionName);
    }

    [Fact]
    public async Task Index_Post_SavesRawInput_ToDatabase()
    {
        var model = new ContactViewModel
        {
            Name = "Test <User>",
            Email = "test@test.com",
            Message = "Hello & goodbye",
            RecaptchaToken = "valid-token"
        };

        await _controller.Index(model);

        var inquiry = _context.ContactInquiries.First();
        Assert.Equal("Test <User>", inquiry.Name);
        Assert.Equal("Hello & goodbye", inquiry.Message);
    }

    #endregion

    #region ThankYou Tests

    [Fact]
    public void ThankYou_ReturnsView()
    {
        var result = _controller.ThankYou();

        Assert.IsType<ViewResult>(result);
    }

    #endregion
}
