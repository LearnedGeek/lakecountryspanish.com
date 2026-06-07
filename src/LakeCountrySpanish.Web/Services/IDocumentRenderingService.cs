namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Renders curriculum Markdown into themed HTML for in-browser preview and
/// browser-print-to-PDF. Phase 3 ships only the HTML path; QuestPDF-backed
/// PDF generation arrives in Phase 4 (see docs/curriculum-system/rendering-and-media-design.md).
/// </summary>
public interface IDocumentRenderingService
{
    /// <summary>
    /// Parses YAML frontmatter + Markdown body, runs the Markdig pipeline with
    /// LCS custom container handling, and returns the rendered HTML plus the
    /// structured frontmatter.
    /// </summary>
    RenderedDocument Render(string markdownSource, string themeName = "teacher-binder");
}

public sealed record RenderedDocument(
    DocumentFrontmatter Frontmatter,
    string BodyHtml,
    string ThemeName);

/// <summary>
/// Curriculum-document frontmatter fields. Only the LCS-specific ones are typed;
/// extra YAML keys land in <see cref="Extras"/> so authors can add metadata
/// without us racing to model every field.
/// </summary>
public sealed class DocumentFrontmatter
{
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? GradeBand { get; set; }
    public string? Level { get; set; }
    public int? DurationMin { get; set; }
    public string? Theme { get; set; }
    public string? Unit { get; set; }
    public List<string> WiStandards { get; set; } = new();
    public List<string> Materials { get; set; } = new();
    public string? Accent { get; set; }
    public string? MascotPose { get; set; }
    public List<ArtifactReference> Artifacts { get; set; } = new();
    public Dictionary<string, object?> Extras { get; set; } = new();
}

public sealed class ArtifactReference
{
    public string? Type { get; set; }
    public string? Slug { get; set; }
    public string? Title { get; set; }
}
