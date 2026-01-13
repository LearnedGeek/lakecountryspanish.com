using Microsoft.AspNetCore.Mvc.ViewComponents;
using LakeCountrySpanish.Web.ViewComponents;

namespace LakeCountrySpanish.Tests.ViewComponents;

public class DataTableViewComponentTests
{
    private readonly DataTableViewComponent _component;

    public DataTableViewComponentTests()
    {
        _component = new DataTableViewComponent();
    }

    [Fact]
    public void Invoke_WithDefaults_ReturnsViewResult()
    {
        // Act
        var result = _component.Invoke();

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<DataTableViewModel>(viewResult.ViewData?.Model);
        Assert.True(model.Striped);
        Assert.True(model.Hover);
        Assert.False(model.Compact);
    }

    [Fact]
    public void Invoke_WithCompact_SetsCompactTrue()
    {
        // Act
        var result = _component.Invoke(compact: true);

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<DataTableViewModel>(viewResult.ViewData?.Model);
        Assert.True(model.Compact);
    }

    [Fact]
    public void Invoke_WithId_SetsId()
    {
        // Act
        var result = _component.Invoke(id: "users-table");

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<DataTableViewModel>(viewResult.ViewData?.Model);
        Assert.Equal("users-table", model.Id);
    }

    [Fact]
    public void DataTableViewModel_HeaderCellClasses_ReturnsCorrectClasses()
    {
        // Arrange
        var model = new DataTableViewModel { Compact = false };

        // Assert
        Assert.Contains("px-6 py-3", model.HeaderCellClasses);
        Assert.Contains("uppercase", model.HeaderCellClasses);
    }

    [Fact]
    public void DataTableViewModel_CompactHeaderCellClasses_ReturnsSmallerPadding()
    {
        // Arrange
        var model = new DataTableViewModel { Compact = true };

        // Assert
        Assert.Contains("px-4 py-2", model.HeaderCellClasses);
    }

    [Fact]
    public void DataTableViewModel_RowClasses_IncludesStripedAndHover()
    {
        // Arrange
        var model = new DataTableViewModel { Striped = true, Hover = true };

        // Assert
        Assert.Contains("even:bg-gray-50", model.RowClasses);
        Assert.Contains("hover:bg-gray-100", model.RowClasses);
    }

    [Fact]
    public void DataTableViewModel_NoStriped_ExcludesStripedClass()
    {
        // Arrange
        var model = new DataTableViewModel { Striped = false, Hover = true };

        // Assert
        Assert.DoesNotContain("even:bg-gray-50", model.RowClasses);
        Assert.Contains("hover:bg-gray-100", model.RowClasses);
    }

    [Fact]
    public void DataTableViewModel_TableClasses_ContainsBaseClasses()
    {
        // Arrange
        var model = new DataTableViewModel();

        // Assert
        Assert.Contains("min-w-full", model.TableClasses);
        Assert.Contains("divide-y", model.TableClasses);
    }
}
