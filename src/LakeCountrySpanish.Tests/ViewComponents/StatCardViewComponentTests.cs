using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class StatCardViewComponentTests
{
    private readonly StatCardViewComponent _component;

    public StatCardViewComponentTests()
    {
        _component = new StatCardViewComponent();
    }

    [Fact]
    public void Invoke_WithRequiredParams_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke("42", "Active Users");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<StatCardViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("42", model.Value);
        Assert.Equal("Active Users", model.Label);
        Assert.Equal("indigo", model.Color);
    }

    [Fact]
    public void Invoke_WithHref_SetsIsLinkTrue()
    {
        // Act
        var result = _component.Invoke("10", "Items", href: "/items");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<StatCardViewModel>(viewResult.ViewData?.Model);
        Assert.True(model.IsLink);
        Assert.Equal("/items", model.Href);
    }

    [Fact]
    public void Invoke_WithSubtitle_SetsSubtitle()
    {
        // Act
        var result = _component.Invoke("$1,234", "Revenue", subtitle: "+12% from last month");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<StatCardViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("+12% from last month", model.Subtitle);
    }

    [Theory]
    [InlineData("blue", "bg-blue-100")]
    [InlineData("green", "bg-green-100")]
    [InlineData("red", "bg-red-100")]
    [InlineData("indigo", "bg-indigo-100")]
    public void StatCardViewModel_IconBgClass_ReturnsCorrectClass(string color, string expectedClass)
    {
        // Arrange
        var model = new StatCardViewModel { Color = color };

        // Assert
        Assert.Equal(expectedClass, model.IconBgClass);
    }

    [Theory]
    [InlineData("users", "M15 19.128a9.38 9.38 0 002.625.372")]
    [InlineData("calendar", "M6.75 3v2.25M17.25 3v2.25")]
    [InlineData("currency", "M12 6v12m-3-2.818")]
    public void StatCardViewModel_IconSvgPath_ReturnsCorrectPath(string icon, string expectedPathStart)
    {
        // Arrange
        var model = new StatCardViewModel { Icon = icon };

        // Assert
        Assert.NotNull(model.IconSvgPath);
        Assert.StartsWith(expectedPathStart, model.IconSvgPath);
    }

    [Fact]
    public void StatCardViewModel_WithBorderLeft_SetsBorderClass()
    {
        // Arrange
        var model = new StatCardViewModel
        {
            Color = "blue",
            BorderLeft = true
        };

        // Assert
        Assert.Contains("border-l-4", model.BorderClass);
    }
}
