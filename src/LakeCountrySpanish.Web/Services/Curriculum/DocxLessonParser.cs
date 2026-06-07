using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services.Curriculum;

/// <summary>
/// Parses a LCS lesson template .docx into a structured DTO ready for
/// emission into a Day + LessonVideo set + Blocks. Strict against the
/// template format — section headers must use Word Heading 1 style and
/// match a known set of names (Spanish or English). Tables under specific
/// headings (Metadata, Vocabulary, Songs, Standards) have a fixed column
/// shape; prose sections are captured as paragraph runs.
///
/// Authors throw <see cref="DocxParseException"/> on structural problems
/// (missing required heading, malformed table). The admin upload action
/// catches these and surfaces a per-field error message rather than a
/// 500.
/// </summary>
public sealed class DocxLessonParser
{
    private static readonly StringComparer HeadingCompare = StringComparer.OrdinalIgnoreCase;

    // Canonical heading names → field. Both Spanish and English accepted.
    private static readonly Dictionary<string, LessonSection> HeadingMap = new(HeadingCompare)
    {
        ["Metadata"]     = LessonSection.Metadata,
        ["Metadatos"]    = LessonSection.Metadata,
        ["Vocabulary"]   = LessonSection.Vocabulary,
        ["Vocabulario"]  = LessonSection.Vocabulary,
        ["Materials"]    = LessonSection.Materials,
        ["Materiales"]   = LessonSection.Materials,
        ["Songs"]        = LessonSection.Songs,
        ["Canciones"]    = LessonSection.Songs,
        ["Standards"]    = LessonSection.Standards,
        ["Estándares"]   = LessonSection.Standards,
        ["Apertura"]     = LessonSection.Apertura,
        ["Opening"]      = LessonSection.Apertura,
        ["Presentación"] = LessonSection.Presentacion,
        ["Presentation"] = LessonSection.Presentacion,
        ["Extensión"]    = LessonSection.Extension,
        ["Extension"]    = LessonSection.Extension,
        ["Actividad"]    = LessonSection.Actividad,
        ["Activity"]     = LessonSection.Actividad,
        ["Juegos"]       = LessonSection.Juegos,
        ["Games"]        = LessonSection.Juegos,
        ["Cultura"]      = LessonSection.Cultura,
        ["Culture"]      = LessonSection.Cultura,
        ["Cierre"]       = LessonSection.Cierre,
        ["Closing"]      = LessonSection.Cierre
    };

    public ParsedLesson Parse(Stream docxStream)
    {
        using var doc = WordprocessingDocument.Open(docxStream, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body
                   ?? throw new DocxParseException("Document has no body.");

        var parsed = new ParsedLesson();
        var currentSection = LessonSection.None;
        var sectionBuffer = new List<OpenXmlElement>();

        void Flush()
        {
            if (currentSection == LessonSection.None || sectionBuffer.Count == 0) return;
            DispatchSection(parsed, currentSection, sectionBuffer);
            sectionBuffer.Clear();
        }

        foreach (var child in body.ChildElements)
        {
            // Heading 1 paragraph → start a new section.
            if (child is Paragraph p && IsHeading1(p))
            {
                var text = ParagraphText(p).Trim();
                if (HeadingMap.TryGetValue(text, out var section))
                {
                    Flush();
                    currentSection = section;
                    continue;
                }
                // Unknown heading 1 — treat as body content for the current section.
            }

            sectionBuffer.Add(child);
        }
        Flush();

        ValidateRequired(parsed);
        return parsed;
    }

    // -------- per-section dispatch --------

    private static void DispatchSection(ParsedLesson p, LessonSection s, List<OpenXmlElement> els)
    {
        switch (s)
        {
            case LessonSection.Metadata:
                ParseMetadataTable(p, els);
                break;
            case LessonSection.Vocabulary:
                ParseVocabularyTable(p, els);
                break;
            case LessonSection.Materials:
                p.Materials.AddRange(BulletItems(els));
                break;
            case LessonSection.Songs:
                ParseSongsTable(p, els);
                break;
            case LessonSection.Standards:
                ParseStandardsTable(p, els);
                break;
            case LessonSection.Apertura:
            case LessonSection.Presentacion:
            case LessonSection.Extension:
            case LessonSection.Actividad:
            case LessonSection.Juegos:
            case LessonSection.Cultura:
            case LessonSection.Cierre:
                p.BodySections[s] = string.Join("\n\n", els.OfType<Paragraph>()
                    .Select(ParagraphText)
                    .Where(t => !string.IsNullOrWhiteSpace(t)));
                break;
        }
    }

    // -------- table parsers --------

    private static void ParseMetadataTable(ParsedLesson p, List<OpenXmlElement> els)
    {
        var table = els.OfType<Table>().FirstOrDefault();
        if (table is null) return;

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().Select(CellText).ToList();
            if (cells.Count < 2) continue;
            var key = cells[0].Trim();
            var value = cells[1].Trim();
            switch (key.ToLowerInvariant())
            {
                case "title":           p.Title = value; break;
                case "unit":            p.Unit = value; break;
                case "grade band":
                case "grade":           p.GradeBand = value; break;
                case "sessions":        p.Sessions = ParseIntOr(value, 1); break;
                case "trimester":       p.Trimester = ParseIntOr(value, 0); break;
                case "theme":           p.Theme = value; break;
                case "objective":
                case "objetivo":        p.Objective = value; break;
                case "duration minutes":
                case "duration":        p.DurationMinutes = ParseIntOr(value, 30); break;
            }
        }
    }

    private static void ParseVocabularyTable(ParsedLesson p, List<OpenXmlElement> els)
    {
        // Each Vocabulary table has columns: Tier | Word
        // Tier values: core / stretch / challenge (case-insensitive).
        var table = els.OfType<Table>().FirstOrDefault();
        if (table is null) return;

        var rows = table.Elements<TableRow>().ToList();
        // Skip the header row if its first cell is "Tier"/"Word"/"Tier (Core/Stretch/Challenge)".
        var startIndex = rows.Count > 0 && IsHeaderRow(rows[0], "tier", "word") ? 1 : 0;

        for (var i = startIndex; i < rows.Count; i++)
        {
            var cells = rows[i].Elements<TableCell>().Select(CellText).ToList();
            if (cells.Count < 2) continue;
            var tier = cells[0].Trim().ToLowerInvariant();
            var word = cells[1].Trim();
            if (string.IsNullOrWhiteSpace(word)) continue;
            switch (tier)
            {
                case "core":      p.VocabCore.Add(word); break;
                case "stretch":   p.VocabStretch.Add(word); break;
                case "challenge": p.VocabChallenge.Add(word); break;
            }
        }
    }

    private static void ParseSongsTable(ParsedLesson p, List<OpenXmlElement> els)
    {
        // Columns: Title | Role | URL
        var table = els.OfType<Table>().FirstOrDefault();
        if (table is null) return;

        var rows = table.Elements<TableRow>().ToList();
        var startIndex = rows.Count > 0 && IsHeaderRow(rows[0], "title", "role") ? 1 : 0;

        var order = 0;
        for (var i = startIndex; i < rows.Count; i++)
        {
            var cells = rows[i].Elements<TableCell>().Select(CellText).ToList();
            if (cells.Count < 3) continue;
            var title = cells[0].Trim();
            var roleText = cells[1].Trim().ToLowerInvariant();
            var url = cells[2].Trim();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) continue;

            var role = roleText switch
            {
                "opening" or "apertura"        => LessonVideoRole.Opening,
                "presentation" or "presentación" or "presentacion" => LessonVideoRole.Presentation,
                "closing" or "cierre"          => LessonVideoRole.Closing,
                _                              => LessonVideoRole.Supplemental
            };

            p.Songs.Add(new ParsedSong
            {
                Title = title,
                Url = url,
                Role = role,
                DisplayOrder = order++
            });
        }
    }

    private static void ParseStandardsTable(ParsedLesson p, List<OpenXmlElement> els)
    {
        // Columns: WI Code | ACTFL | Practice | Indicator | How Addressed
        var table = els.OfType<Table>().FirstOrDefault();
        if (table is null) return;

        var rows = table.Elements<TableRow>().ToList();
        var startIndex = rows.Count > 0 && IsHeaderRow(rows[0], "wi", "actfl") ? 1 : 0;

        for (var i = startIndex; i < rows.Count; i++)
        {
            var cells = rows[i].Elements<TableCell>().Select(CellText).ToList();
            if (cells.Count < 1) continue;
            var code = cells[0].Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;
            p.StandardCodes.Add(code);
        }
    }

    // -------- helpers --------

    private static bool IsHeading1(Paragraph p)
    {
        var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (string.IsNullOrEmpty(styleId)) return false;
        return styleId.Equals("Heading1", StringComparison.OrdinalIgnoreCase)
            || styleId.Equals("Heading 1", StringComparison.OrdinalIgnoreCase);
    }

    private static string ParagraphText(Paragraph p) =>
        string.Concat(p.Descendants<Text>().Select(t => t.Text));

    private static string CellText(TableCell c) =>
        string.Concat(c.Descendants<Text>().Select(t => t.Text));

    private static IEnumerable<string> BulletItems(List<OpenXmlElement> els) =>
        els.OfType<Paragraph>()
           .Select(ParagraphText)
           .Where(t => !string.IsNullOrWhiteSpace(t));

    private static bool IsHeaderRow(TableRow row, params string[] expectedCellPrefixes)
    {
        var cells = row.Elements<TableCell>().Select(c => CellText(c).Trim().ToLowerInvariant()).ToList();
        if (cells.Count < expectedCellPrefixes.Length) return false;
        for (var i = 0; i < expectedCellPrefixes.Length; i++)
        {
            if (!cells[i].StartsWith(expectedCellPrefixes[i])) return false;
        }
        return true;
    }

    private static int ParseIntOr(string s, int fallback) =>
        int.TryParse(s, out var n) ? n : fallback;

    private static void ValidateRequired(ParsedLesson p)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Title)) problems.Add("Metadata table is missing a Title.");
        if (string.IsNullOrWhiteSpace(p.Unit)) problems.Add("Metadata table is missing a Unit.");
        if (p.VocabCore.Count == 0) problems.Add("Vocabulary table has no Core terms — at least one is required.");
        if (problems.Count > 0)
        {
            throw new DocxParseException(string.Join(" ", problems));
        }
    }
}

public enum LessonSection
{
    None,
    Metadata,
    Vocabulary,
    Materials,
    Songs,
    Standards,
    Apertura,
    Presentacion,
    Extension,
    Actividad,
    Juegos,
    Cultura,
    Cierre
}

public sealed class DocxParseException : Exception
{
    public DocxParseException(string message) : base(message) { }
}
