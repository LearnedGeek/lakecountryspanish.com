using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class DataTableViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        bool striped = true,
        bool hover = true,
        bool compact = false,
        string? id = null)
    {
        return View(new DataTableViewModel
        {
            Striped = striped,
            Hover = hover,
            Compact = compact,
            Id = id
        });
    }
}

public class DataTableViewModel
{
    public bool Striped { get; set; } = true;
    public bool Hover { get; set; } = true;
    public bool Compact { get; set; }
    public string? Id { get; set; }

    public string TableClasses
    {
        get
        {
            var classes = "min-w-full divide-y divide-gray-200";
            return classes;
        }
    }

    public string HeaderCellClasses => Compact
        ? "px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
        : "px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider";

    public string BodyCellClasses => Compact
        ? "px-4 py-2 whitespace-nowrap text-sm text-gray-900"
        : "px-6 py-4 whitespace-nowrap text-sm text-gray-900";

    public string RowClasses
    {
        get
        {
            var classes = new List<string>();
            if (Striped) classes.Add("even:bg-gray-50");
            if (Hover) classes.Add("hover:bg-gray-100");
            return string.Join(" ", classes);
        }
    }
}
