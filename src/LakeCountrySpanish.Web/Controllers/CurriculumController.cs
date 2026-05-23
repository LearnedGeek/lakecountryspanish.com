using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class CurriculumController : Controller
{
    private readonly IDocumentRenderingService _renderer;
    private readonly IWebHostEnvironment _environment;

    public CurriculumController(IDocumentRenderingService renderer, IWebHostEnvironment environment)
    {
        _renderer = renderer;
        _environment = environment;
    }

    /// <summary>
    /// Dev-only preview of the Los Colores sample doc. Reads the markdown from
    /// the solution-root docs/ folder; once Karen is authoring against real
    /// entities, the input will come from the DB instead of disk.
    /// </summary>
    public IActionResult PreviewSample()
    {
        var samplePath = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath, "..", "..",
            "docs", "curriculum-system", "samples", "los-colores.md"));

        if (!System.IO.File.Exists(samplePath))
        {
            return NotFound($"Sample doc not found at {samplePath}");
        }

        var markdown = System.IO.File.ReadAllText(samplePath);
        var rendered = _renderer.Render(markdown);

        return View(rendered);
    }
}
