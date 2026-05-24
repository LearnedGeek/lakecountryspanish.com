using System.Text;
using System.Text.Json;

namespace LakeCountrySpanish.Web.Services.Curriculum.Blocks;

public sealed class BlockCompiler : IBlockCompiler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string Serialize(IReadOnlyList<Block> blocks) =>
        JsonSerializer.Serialize(blocks, JsonOpts);

    public IReadOnlyList<Block> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Block>();
        try
        {
            return JsonSerializer.Deserialize<List<Block>>(json, JsonOpts) ?? new List<Block>();
        }
        catch (JsonException)
        {
            // Malformed JSON should not crash the editor; treat as empty so the
            // author can re-author rather than seeing a 500 page.
            return Array.Empty<Block>();
        }
    }

    public string Compile(IReadOnlyList<Block> blocks)
    {
        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock p:        AppendParagraph(sb, p); break;
                case VocabBlock v:            AppendVocab(sb, v); break;
                case RawMarkdownBlock r:      AppendRaw(sb, r); break;
                default:                      /* unknown block — skip */ break;
            }
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    private static void AppendParagraph(StringBuilder sb, ParagraphBlock p)
    {
        var text = (p.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(p.ContainerKind))
        {
            sb.AppendLine(text);
            return;
        }

        var open = $":::{p.ContainerKind.Trim()}";
        if (!string.IsNullOrWhiteSpace(p.Time))
        {
            open += $" {{time=\"{EscapeAttr(p.Time)}\"}}";
        }
        sb.AppendLine(open);
        sb.AppendLine(text);
        sb.AppendLine(":::");
    }

    private static void AppendVocab(StringBuilder sb, VocabBlock v)
    {
        sb.AppendLine(":::vocab");
        sb.AppendLine("| Español | English | Pronunciation | Cue |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var row in v.Rows)
        {
            sb.Append("| ").Append(SanitizeCell(row.Spanish))
              .Append(" | ").Append(SanitizeCell(row.English))
              .Append(" | ").Append(SanitizeCell(row.Pronunciation))
              .Append(" | ").Append(SanitizeCell(row.Cue))
              .AppendLine(" |");
        }
        sb.AppendLine(":::");
    }

    private static void AppendRaw(StringBuilder sb, RawMarkdownBlock r) =>
        sb.AppendLine(r.Markdown?.TrimEnd() ?? string.Empty);

    private static string SanitizeCell(string? value) =>
        (value ?? string.Empty).Replace("|", "\\|").Replace("\r", "").Replace("\n", " ").Trim();

    private static string EscapeAttr(string value) =>
        value.Replace("\"", "\\\"");
}
