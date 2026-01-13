using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class StatusBadgeViewComponentTests
{
    private readonly StatusBadgeViewComponent _component;

    public StatusBadgeViewComponentTests()
    {
        _component = new StatusBadgeViewComponent();
    }

    [Fact]
    public void Invoke_WithText_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke("Active");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<StatusBadgeViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("Active", model.Text);
        Assert.Equal("gray", model.Color);
        Assert.Equal("sm", model.Size);
    }

    [Theory]
    [InlineData("green", "bg-green-100 text-green-800")]
    [InlineData("red", "bg-red-100 text-red-800")]
    [InlineData("yellow", "bg-yellow-100 text-yellow-800")]
    [InlineData("blue", "bg-blue-100 text-blue-800")]
    [InlineData("indigo", "bg-indigo-100 text-indigo-800")]
    [InlineData("gray", "bg-gray-100 text-gray-800")]
    public void StatusBadgeViewModel_ColorClasses_ReturnsCorrectClasses(string color, string expectedClasses)
    {
        // Arrange
        var model = new StatusBadgeViewModel { Color = color };

        // Assert
        Assert.Equal(expectedClasses, model.ColorClasses);
    }

    [Theory]
    [InlineData("xs", "px-1.5 py-0.5 text-xs")]
    [InlineData("sm", "px-2.5 py-0.5 text-xs")]
    [InlineData("md", "px-3 py-1 text-sm")]
    [InlineData("lg", "px-4 py-1.5 text-base")]
    public void StatusBadgeViewModel_SizeClasses_ReturnsCorrectClasses(string size, string expectedClasses)
    {
        // Arrange
        var model = new StatusBadgeViewModel { Size = size };

        // Assert
        Assert.Equal(expectedClasses, model.SizeClasses);
    }

    [Fact]
    public void StatusBadgeViewModel_AllClasses_ContainsBaseAndColorAndSize()
    {
        // Arrange
        var model = new StatusBadgeViewModel
        {
            Text = "Test",
            Color = "green",
            Size = "md"
        };

        // Assert
        Assert.Contains("inline-flex", model.AllClasses);
        Assert.Contains("rounded-full", model.AllClasses);
        Assert.Contains("bg-green-100", model.AllClasses);
        Assert.Contains("px-3", model.AllClasses);
    }

    [Fact]
    public void Invoke_ConvertsColorToLowerCase()
    {
        // Act
        var result = _component.Invoke("Status", color: "GREEN");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<StatusBadgeViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("green", model.Color);
    }
}
