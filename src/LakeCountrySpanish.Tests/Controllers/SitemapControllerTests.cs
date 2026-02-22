using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using LakeCountrySpanish.Web.Controllers;

namespace LakeCountrySpanish.Tests.Controllers;

public class SitemapControllerTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly SitemapController _controller;

    public SitemapControllerTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["AppSettings:BaseUrl"])
            .Returns("https://lakecountryspanish.com");

        _controller = new SitemapController(_configurationMock.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #region Index Tests

    [Fact]
    public void Index_ReturnsXmlContent()
    {
        var result = _controller.Index();

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.StartsWith("application/xml", contentResult.ContentType);
    }

    [Fact]
    public void Index_ContainsRequiredUrls()
    {
        var result = _controller.Index();

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Contains("lakecountryspanish.com", contentResult.Content);
        Assert.Contains("<url>", contentResult.Content);
        Assert.Contains("<loc>", contentResult.Content);
    }

    [Fact]
    public void Index_ContainsSitemapNamespace()
    {
        var result = _controller.Index();

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Contains("sitemaps.org", contentResult.Content);
    }

    #endregion
}
