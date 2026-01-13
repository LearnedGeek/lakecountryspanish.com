using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class EmptyStateViewComponentTests
{
    private readonly EmptyStateViewComponent _component;

    public EmptyStateViewComponentTests()
    {
        _component = new EmptyStateViewComponent();
    }

    [Fact]
    public void Invoke_WithTitle_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke("No items found");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<EmptyStateViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("No items found", model.Title);
        Assert.Equal("folder", model.Icon);
    }

    [Fact]
    public void Invoke_WithMessage_SetsMessage()
    {
        // Act
        var result = _component.Invoke("No users", message: "Add your first user to get started");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<EmptyStateViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("Add your first user to get started", model.Message);
    }

    [Fact]
    public void Invoke_WithAction_SetsHasActionTrue()
    {
        // Act
        var result = _component.Invoke("No items", actionText: "Add Item", actionUrl: "/items/create");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<EmptyStateViewModel>(viewResult.ViewData?.Model);
        Assert.True(model.HasAction);
        Assert.Equal("Add Item", model.ActionText);
        Assert.Equal("/items/create", model.ActionUrl);
    }

    [Fact]
    public void Invoke_WithoutAction_SetsHasActionFalse()
    {
        // Act
        var result = _component.Invoke("Empty");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<EmptyStateViewModel>(viewResult.ViewData?.Model);
        Assert.False(model.HasAction);
    }

    [Theory]
    [InlineData("document")]
    [InlineData("users")]
    [InlineData("calendar")]
    [InlineData("inbox")]
    [InlineData("trophy")]
    public void EmptyStateViewModel_IconSvgPath_ReturnsPathForKnownIcons(string icon)
    {
        // Arrange
        var model = new EmptyStateViewModel { Icon = icon };

        // Assert
        Assert.NotNull(model.IconSvgPath);
        Assert.NotEmpty(model.IconSvgPath);
    }

    [Fact]
    public void EmptyStateViewModel_UnknownIcon_ReturnsFolderPath()
    {
        // Arrange
        var model = new EmptyStateViewModel { Icon = "unknown-icon" };

        // Assert
        Assert.NotNull(model.IconSvgPath);
        Assert.Contains("M2.25 12.75V12A2.25", model.IconSvgPath); // folder path
    }
}
