using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using YamlDotNet.RepresentationModel;

namespace LakeCountrySpanish.Web.Services;

public sealed class DocumentRenderingService : IDocumentRenderingService
{
    private readonly MarkdownPipeline _pipeline;

    public DocumentRenderingService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseCustomContainers()
            .UseGenericAttributes()
            .Build();
    }

    public RenderedDocument Render(string markdownSource, string themeName = "lcs-k2")
    {
        var (frontmatterYaml, body) = SplitFrontmatter(markdownSource);
        var frontmatter = ParseFrontmatter(frontmatterYaml);

        var document = Markdown.Parse(body, _pipeline);
        RewriteCustomContainers(document);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        _pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        return new RenderedDocument(frontmatter, writer.ToString(), themeName);
    }

    /// <summary>
    /// Maps :::block-name into &lt;section class="block block-name"&gt; with any
    /// attributes (time, type, prints, ...) flattened to data-* attributes so
    /// the CSS can style them and the theme can react to game/worksheet variants.
    /// </summary>
    private static void RewriteCustomContainers(MarkdownDocument document)
    {
        foreach (var container in document.Descendants<CustomContainer>())
        {
            var attrs = container.GetAttributes();
            var name = (container.Info ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(name)) continue;

            attrs.AddClass("block");
            attrs.AddClass($"block-{name}");

            // UseGenericAttributes() lands {key="value"} pairs as raw attributes
            // (e.g. time="3 min"). Rename those to data-* so they're valid HTML
            // and the CSS [data-time] selector matches.
            if (attrs.Properties is { Count: > 0 })
            {
                for (var i = 0; i < attrs.Properties.Count; i++)
                {
                    var (key, value) = attrs.Properties[i];
                    if (key == "id" || key == "class" || key.StartsWith("data-", StringComparison.Ordinal))
                        continue;
                    attrs.Properties[i] = new KeyValuePair<string, string?>($"data-{key}", value);
                }
            }
        }
    }

    private static (string yaml, string body) SplitFrontmatter(string source)
    {
        if (!source.StartsWith("---", StringComparison.Ordinal))
            return (string.Empty, source);

        var newlineAfterOpen = source.IndexOf('\n');
        if (newlineAfterOpen < 0) return (string.Empty, source);

        var closeFence = source.IndexOf("\n---", newlineAfterOpen, StringComparison.Ordinal);
        if (closeFence < 0) return (string.Empty, source);

        var yaml = source[(newlineAfterOpen + 1)..closeFence];
        var afterFence = source.IndexOf('\n', closeFence + 1);
        var body = afterFence < 0 ? string.Empty : source[(afterFence + 1)..];
        return (yaml, body);
    }

    private static DocumentFrontmatter ParseFrontmatter(string yaml)
    {
        var fm = new DocumentFrontmatter();
        if (string.IsNullOrWhiteSpace(yaml)) return fm;

        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);
        if (stream.Documents.Count == 0) return fm;

        if (stream.Documents[0].RootNode is not YamlMappingNode root) return fm;

        foreach (var (keyNode, valueNode) in root.Children)
        {
            if (keyNode is not YamlScalarNode keyScalar || keyScalar.Value is null) continue;
            var key = keyScalar.Value;

            switch (key)
            {
                case "title":         fm.Title = AsString(valueNode); break;
                case "slug":          fm.Slug = AsString(valueNode); break;
                case "grade_band":    fm.GradeBand = AsString(valueNode); break;
                case "level":         fm.Level = AsString(valueNode); break;
                case "duration_min":  fm.DurationMin = AsInt(valueNode); break;
                case "theme":         fm.Theme = AsString(valueNode); break;
                case "unit":          fm.Unit = AsString(valueNode); break;
                case "accent":        fm.Accent = AsString(valueNode); break;
                case "mascot_pose":   fm.MascotPose = AsString(valueNode); break;
                case "wi_standards":  fm.WiStandards = AsStringList(valueNode); break;
                case "materials":     fm.Materials = AsStringList(valueNode); break;
                case "artifacts":     fm.Artifacts = AsArtifactList(valueNode); break;
                default:              fm.Extras[key] = AsString(valueNode); break;
            }
        }

        return fm;
    }

    private static string? AsString(YamlNode node) => node is YamlScalarNode s ? s.Value : null;

    private static int? AsInt(YamlNode node) =>
        node is YamlScalarNode s && int.TryParse(s.Value, out var n) ? n : null;

    private static List<string> AsStringList(YamlNode node)
    {
        if (node is not YamlSequenceNode seq) return new();
        var list = new List<string>(seq.Children.Count);
        foreach (var child in seq.Children)
        {
            if (child is YamlScalarNode s && s.Value is { } v) list.Add(v);
        }
        return list;
    }

    private static List<ArtifactReference> AsArtifactList(YamlNode node)
    {
        if (node is not YamlSequenceNode seq) return new();
        var list = new List<ArtifactReference>(seq.Children.Count);
        foreach (var child in seq.Children)
        {
            if (child is not YamlMappingNode m) continue;
            var ar = new ArtifactReference();
            foreach (var (k, v) in m.Children)
            {
                if (k is not YamlScalarNode ks) continue;
                switch (ks.Value)
                {
                    case "type":  ar.Type = AsString(v); break;
                    case "slug":  ar.Slug = AsString(v); break;
                    case "title": ar.Title = AsString(v); break;
                }
            }
            list.Add(ar);
        }
        return list;
    }
}
