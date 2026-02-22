using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Security.Claims;

namespace LakeCountrySpanish.Tests.Controllers;

public class TokenControllerTests : IDisposable
{
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<TokenController>> _loggerMock;
    private readonly TokenController _controller;
    private readonly string _testUserId;

    public TokenControllerTests()
    {
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<TokenController>>();

        _controller = new TokenController(_tokenServiceMock.Object, _loggerMock.Object);

        _testUserId = Guid.NewGuid().ToString();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.TempData = tempData;

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _testUserId) };
        var identity = new ClaimsIdentity(claims, "Test");
        httpContext.User = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock.Setup(u => u.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()))
            .Returns("https://localhost/test");
        _controller.Url = urlHelperMock.Object;
    }

    public void Dispose() { }

    #region Index Tests

    [Fact]
    public async Task Index_ReturnsView_WithTokenData()
    {
        _tokenServiceMock.Setup(s => s.GetEarnedTokenBalanceAsync(_testUserId))
            .ReturnsAsync(10);
        _tokenServiceMock.Setup(s => s.GetPurchasedTokenBalanceAsync(_testUserId))
            .ReturnsAsync(5);
        _tokenServiceMock.Setup(s => s.GetActivePermissionAsync(_testUserId))
            .ReturnsAsync((TokenPurchasePermission?)null);
        _tokenServiceMock.Setup(s => s.GetActiveTokensAsync(_testUserId))
            .ReturnsAsync(new List<Token>());
        _tokenServiceMock.Setup(s => s.GetTransactionHistoryAsync(_testUserId, 20))
            .ReturnsAsync(new List<TokenTransaction>());

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<TokenViewModel>(viewResult.Model);
    }

    #endregion

    #region Purchase Tests

    [Fact]
    public async Task Purchase_RedirectsWithError_WhenQuantityInvalid()
    {
        var result = await _controller.Purchase(0);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Purchase_RedirectsWithError_WhenQuantityTooHigh()
    {
        var result = await _controller.Purchase(11);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Purchase_RedirectsWithError_WhenCannotPurchase()
    {
        _tokenServiceMock.Setup(s => s.CanPurchaseTokensAsync(_testUserId))
            .ReturnsAsync(false);

        var result = await _controller.Purchase(5);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Contains("Error", _controller.TempData.Keys);
    }

    [Fact]
    public async Task Purchase_RedirectsToCheckout_WhenValid()
    {
        _tokenServiceMock.Setup(s => s.CanPurchaseTokensAsync(_testUserId))
            .ReturnsAsync(true);
        _tokenServiceMock.Setup(s => s.GetActivePermissionAsync(_testUserId))
            .ReturnsAsync(new TokenPurchasePermission { Id = 1, StudentId = _testUserId, TokenLimit = 10, TokensPurchased = 0 });
        _tokenServiceMock.Setup(s => s.CreateTokenPurchaseCheckoutAsync(
                _testUserId, 5, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://checkout.stripe.com/sess_123");

        var result = await _controller.Purchase(5);

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("stripe.com", redirectResult.Url);
    }

    #endregion

    #region History Tests

    [Fact]
    public async Task History_ReturnsView_WithTransactions()
    {
        _tokenServiceMock.Setup(s => s.GetTransactionHistoryAsync(_testUserId, null))
            .ReturnsAsync(new List<TokenTransaction>());

        var result = await _controller.History();

        Assert.IsType<ViewResult>(result);
    }

    #endregion
}
