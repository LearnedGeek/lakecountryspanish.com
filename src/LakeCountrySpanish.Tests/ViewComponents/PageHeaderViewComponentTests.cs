using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class PageHeaderViewComponentTests
{
    private readonly PageHeaderViewComponent _component;

    public PageHeaderViewComponentTests()
    {
        _component = new PageHeaderViewComponent();
    }

    [Fact]
    public void Invoke_WithTitle_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke("Dashboard");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<PageHeaderViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("Dashboard", model.Title);
        Assert.Null(model.Subtitle);
        Assert.Null(model.BackUrl);
    }

    [Fact]
    public void Invoke_WithSubtitle_SetsSubtitle()
    {
        // Act
        var result = _component.Invoke("Users", subtitle: "Manage all users");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<PageHeaderViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("Users", model.Title);
        Assert.Equal("Manage all users", model.Subtitle);
    }

    [Fact]
    public void Invoke_WithBackUrl_SetsHasBackLinkTrue()
    {
        // Act
        var result = _component.Invoke("Edit User", backUrl: "/users");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<PageHeaderViewModel>(viewResult.ViewData?.Model);
        Assert.True(model.HasBackLink);
        Assert.Equal("/users", model.BackUrl);
    }

    [Fact]
    public void Invoke_WithBackText_SetsBackText()
    {
        // Act
        var result = _component.Invoke("Details", backUrl: "/list", backText: "Return to List");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<PageHeaderViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("Return to List", model.BackText);
    }

    [Fact]
    public void Invoke_WithoutBackUrl_SetsHasBackLinkFalse()
    {
        // Act
        var result = _component.Invoke("Home");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<PageHeaderViewModel>(viewResult.ViewData?.Model);
        Assert.False(model.HasBackLink);
    }

    [Fact]
    public void PageHeaderViewModel_DefaultBackText_IsBack()
    {
        // Arrange
        var model = new PageHeaderViewModel();

        // Assert
        Assert.Equal("Back", model.BackText);
    }
}
