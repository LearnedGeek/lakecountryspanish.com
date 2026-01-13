using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class CardViewComponentTests
{
    private readonly CardViewComponent _component;

    public CardViewComponentTests()
    {
        _component = new CardViewComponent();
    }

    [Fact]
    public void Invoke_WithDefaults_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke();

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<CardViewModel>(viewResult.ViewData?.Model);
        Assert.Null(model.Title);
        Assert.False(model.NoPadding);
        Assert.False(model.HasHeader);
    }

    [Fact]
    public void Invoke_WithTitle_SetsHasHeaderTrue()
    {
        // Act
        var result = _component.Invoke(title: "Card Title");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<CardViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("Card Title", model.Title);
        Assert.True(model.HasHeader);
    }

    [Fact]
    public void Invoke_WithNoPadding_SetsEmptyBodyPadding()
    {
        // Act
        var result = _component.Invoke(noPadding: true);

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<CardViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("", model.BodyPadding);
    }

    [Fact]
    public void Invoke_WithPadding_SetsBodyPadding()
    {
        // Act
        var result = _component.Invoke(noPadding: false);

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<CardViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("p-6", model.BodyPadding);
    }

    [Fact]
    public void Invoke_WithHeaderAction_SetsHasHeaderActionTrue()
    {
        // Act
        var result = _component.Invoke(
            title: "Items",
            headerAction: "View All",
            headerActionUrl: "/items"
        );

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<CardViewModel>(viewResult.ViewData?.Model);
        Assert.True(model.HasHeaderAction);
        Assert.Equal("View All", model.HeaderAction);
        Assert.Equal("/items", model.HeaderActionUrl);
    }

    [Fact]
    public void Invoke_WithCssClass_SetsCssClass()
    {
        // Act
        var result = _component.Invoke(cssClass: "mt-4 shadow-lg");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<CardViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("mt-4 shadow-lg", model.CssClass);
    }
}
