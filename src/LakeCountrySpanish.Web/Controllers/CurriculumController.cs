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
        var markdown = ReadSampleOrNull("los-colores.md");
        if (markdown is null) return NotFound("Sample doc los-colores.md not found.");

        return View(_renderer.Render(markdown));
    }

    /// <summary>
    /// Assembles a full teacher binder by rendering the lesson plan plus every
    /// artifact named in its frontmatter <c>artifacts:</c> array. Output is a
    /// single printable HTML document with a cover, section separators, and
    /// CSS page-break rules so teachers can browser-print the whole binder.
    ///
    /// Per Karen's requirement: the binder is not just the lesson plan — it
    /// must include every referenced printable (bingo cards, worksheets,
    /// coloring pages, song lyrics) in print-ready form, default selected.
    /// </summary>
    public IActionResult PreviewBinder(string slug = "los-colores")
    {
        var lessonMd = ReadSampleOrNull($"{slug}.md");
        if (lessonMd is null) return NotFound($"Lesson {slug}.md not found.");

        var lesson = _renderer.Render(lessonMd);

        var artifacts = new List<RenderedDocument>();
        foreach (var reference in lesson.Frontmatter.Artifacts)
        {
            if (string.IsNullOrWhiteSpace(reference.Slug)) continue;
            var artifactMd = ReadSampleOrNull($"{reference.Slug}.md");
            if (artifactMd is null) continue;
            artifacts.Add(_renderer.Render(artifactMd));
        }

        return View(new Models.ViewModels.CurriculumBinderViewModel
        {
            Lesson = lesson,
            Artifacts = artifacts,
            TeacherName = User.Identity?.Name ?? "Teacher",
            GeneratedAt = DateTime.UtcNow
        });
    }

    private string? ReadSampleOrNull(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath, "..", "..",
            "docs", "curriculum-system", "samples", fileName));
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
    }
}
