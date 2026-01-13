using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class AlertViewComponentTests
{
    private readonly AlertViewComponent _component;

    public AlertViewComponentTests()
    {
        _component = new AlertViewComponent();
    }

    [Fact]
    public void Invoke_WithEmptyMessage_ReturnsEmptyContent()
    {
        // Act
        var result = _component.Invoke("success", null);

        // Assert
        Assert.IsType<ContentViewComponentResult>(result);
    }

    [Fact]
    public void Invoke_WithValidMessage_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke("success", "Test message");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<AlertViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("success", model.Type);
        Assert.Equal("Test message", model.Message);
        Assert.True(model.Dismissible);
    }

    [Theory]
    [InlineData("success", "bg-green-100 border-green-400 text-green-700")]
    [InlineData("error", "bg-red-100 border-red-400 text-red-700")]
    [InlineData("warning", "bg-yellow-100 border-yellow-400 text-yellow-700")]
    [InlineData("info", "bg-blue-100 border-blue-400 text-blue-700")]
    [InlineData("unknown", "bg-gray-100 border-gray-400 text-gray-700")]
    public void AlertViewModel_BackgroundColor_ReturnsCorrectClasses(string type, string expectedClasses)
    {
        // Arrange
        var model = new AlertViewModel { Type = type };

        // Assert
        Assert.Equal(expectedClasses, model.BackgroundColor);
    }

    [Fact]
    public void Invoke_WithDismissibleFalse_SetsCorrectProperty()
    {
        // Act
        var result = _component.Invoke("success", "Test", dismissible: false);

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<AlertViewModel>(viewResult.ViewData?.Model);
        Assert.False(model.Dismissible);
    }

    [Fact]
    public void Invoke_ConvertsTypeToLowerCase()
    {
        // Act
        var result = _component.Invoke("SUCCESS", "Test");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<AlertViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("success", model.Type);
    }
}
