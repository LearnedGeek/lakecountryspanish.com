using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class ModalViewComponentTests
{
    private readonly ModalViewComponent _component;

    public ModalViewComponentTests()
    {
        _component = new ModalViewComponent();
    }

    [Fact]
    public void Invoke_WithRequiredParams_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke("my-modal", "Modal Title");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<ModalViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("my-modal", model.Id);
        Assert.Equal("Modal Title", model.Title);
        Assert.Equal("md", model.Size);
        Assert.True(model.CloseOnBackdrop);
    }

    [Theory]
    [InlineData("sm", "max-w-sm")]
    [InlineData("md", "max-w-lg")]
    [InlineData("lg", "max-w-2xl")]
    [InlineData("xl", "max-w-4xl")]
    [InlineData("full", "max-w-full mx-4")]
    public void ModalViewModel_SizeClass_ReturnsCorrectClass(string size, string expectedClass)
    {
        // Arrange
        var model = new ModalViewModel { Size = size };

        // Assert
        Assert.Equal(expectedClass, model.SizeClass);
    }

    [Fact]
    public void Invoke_WithCloseOnBackdropFalse_SetsProperty()
    {
        // Act
        var result = _component.Invoke("modal-id", "Title", closeOnBackdrop: false);

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<ModalViewModel>(viewResult.ViewData?.Model);
        Assert.False(model.CloseOnBackdrop);
    }

    [Fact]
    public void Invoke_ConvertsToLowerCase()
    {
        // Act
        var result = _component.Invoke("test", "Title", size: "LG");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<ModalViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("lg", model.Size);
    }
}
