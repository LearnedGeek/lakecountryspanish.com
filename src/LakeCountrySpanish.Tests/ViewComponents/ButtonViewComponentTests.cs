using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class ButtonViewComponentTests
{
    private readonly ButtonViewComponent _component;

    public ButtonViewComponentTests()
    {
        _component = new ButtonViewComponent();
    }

    [Fact]
    public void Invoke_WithMinimalParams_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke("Click Me");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<ButtonViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("Click Me", model.Text);
        Assert.Equal("primary", model.Type);
        Assert.Equal("md", model.Size);
    }

    [Fact]
    public void Invoke_WithHref_SetsIsLinkTrue()
    {
        // Act
        var result = _component.Invoke("Go", href: "/some-url");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<ButtonViewModel>(viewResult.ViewData?.Model);
        Assert.True(model.IsLink);
        Assert.Equal("/some-url", model.Href);
    }

    [Fact]
    public void Invoke_WithoutHref_SetsIsLinkFalse()
    {
        // Act
        var result = _component.Invoke("Submit", submit: true);

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<ButtonViewModel>(viewResult.ViewData?.Model);
        Assert.False(model.IsLink);
        Assert.True(model.Submit);
    }

    [Theory]
    [InlineData("primary", "border border-transparent text-white bg-indigo-600 hover:bg-indigo-700 focus:ring-indigo-500")]
    [InlineData("secondary", "border border-gray-300 text-gray-700 bg-white hover:bg-gray-50 focus:ring-indigo-500")]
    [InlineData("danger", "border border-transparent text-white bg-red-600 hover:bg-red-700 focus:ring-red-500")]
    [InlineData("success", "border border-transparent text-white bg-green-600 hover:bg-green-700 focus:ring-green-500")]
    public void ButtonViewModel_TypeClasses_ReturnsCorrectClasses(string type, string expectedClasses)
    {
        // Arrange
        var model = new ButtonViewModel { Type = type };

        // Assert
        Assert.Equal(expectedClasses, model.TypeClasses);
    }

    [Theory]
    [InlineData("sm", "px-3 py-1.5 text-xs")]
    [InlineData("md", "px-4 py-2 text-sm")]
    [InlineData("lg", "px-6 py-3 text-base")]
    public void ButtonViewModel_SizeClasses_ReturnsCorrectClasses(string size, string expectedClasses)
    {
        // Arrange
        var model = new ButtonViewModel { Size = size };

        // Assert
        Assert.Equal(expectedClasses, model.SizeClasses);
    }

    [Fact]
    public void ButtonViewModel_WithDisabled_IncludesDisabledClasses()
    {
        // Arrange
        var model = new ButtonViewModel { Disabled = true };

        // Assert
        Assert.Contains("opacity-50", model.DisabledClasses);
        Assert.Contains("cursor-not-allowed", model.DisabledClasses);
    }

    [Theory]
    [InlineData("plus", "M12 4.5v15m7.5-7.5h-15")]
    [InlineData("check", "M4.5 12.75l6 6 9-13.5")]
    [InlineData("arrow-left", "M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18")]
    public void ButtonViewModel_IconSvgPath_ReturnsCorrectPath(string icon, string expectedPath)
    {
        // Arrange
        var model = new ButtonViewModel { Icon = icon };

        // Assert
        Assert.Equal(expectedPath, model.IconSvgPath);
    }
}
