using System.Text.Json.Serialization;

namespace LakeCountrySpanish.Web.Services.Curriculum.Blocks;

/// <summary>
/// Discriminated union of every block type the curriculum block editor can
/// produce. The <c>type</c> JSON property carries the discriminator; specific
/// fields live on the concrete subclass. Persisted as the
/// <c>BodyBlocksJson</c> column on <see cref="Models.Entities.Day"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", IgnoreUnrecognizedTypeDiscriminators = true)]
[JsonDerivedType(typeof(ParagraphBlock),    "paragraph")]
[JsonDerivedType(typeof(VocabBlock),        "vocab")]
[JsonDerivedType(typeof(RawMarkdownBlock),  "raw")]
public abstract class Block
{
    /// <summary>
    /// Stable opaque identifier — HTMX uses it to target individual block
    /// fragments for swap. Generated server-side on insert.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];

    /// <summary>Human-friendly summary for the block list header.</summary>
    [JsonIgnore]
    public abstract string Label { get; }
}

/// <summary>
/// Plain prose. The author types body text; emits as a single Markdown
/// paragraph wrapped in the optional named lesson-flow container (warmup,
/// teach, practice, etc.) when <see cref="ContainerKind"/> is set.
/// </summary>
public sealed class ParagraphBlock : Block
{
    /// <summary>Free-form prose Markdown (inline formatting like **bold** is allowed).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional container name to wrap the prose in. When set to e.g.
    /// "warmup", the compiler emits the text wrapped in :::warmup ... :::.
    /// Use null/empty for un-wrapped prose.
    /// </summary>
    public string? ContainerKind { get; set; }

    /// <summary>
    /// Optional time hint shown as a pill in the rendered output, e.g.
    /// "3 min". Compiles to the {time="..."} attribute on the container.
    /// </summary>
    public string? Time { get; set; }

    public override string Label => string.IsNullOrEmpty(ContainerKind)
        ? "Paragraph"
        : char.ToUpperInvariant(ContainerKind[0]) + ContainerKind[1..];
}

/// <summary>
/// Structured vocabulary table. Stored as rows; compiles to a Markdown table
/// inside :::vocab.
/// </summary>
public sealed class VocabBlock : Block
{
    public List<VocabRow> Rows { get; set; } = new();

    public override string Label => $"Vocabulary ({Rows.Count} row{(Rows.Count == 1 ? "" : "s")})";
}

public sealed class VocabRow
{
    public string Spanish { get; set; } = string.Empty;
    public string English { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string Cue { get; set; } = string.Empty;
}

/// <summary>
/// Escape hatch: arbitrary Markdown the author wrote directly. Used to wrap
/// content imported from external lessons or for one-off blocks the block
/// palette doesn't cover yet.
/// </summary>
public sealed class RawMarkdownBlock : Block
{
    public string Markdown { get; set; } = string.Empty;

    public override string Label => "Raw Markdown";
}
