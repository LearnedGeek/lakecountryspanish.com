using System.Text.Json;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services.Curriculum;

/// <summary>
/// Builds a <see cref="LessonPrintViewModel"/> from a loaded <see cref="Day"/>
/// graph (Unit + WisconsinStandards + Videos). Lives outside the controller
/// so both <c>CurriculumController</c> (admin Review/Print) and
/// <c>PublicCurriculumController</c> (member-facing view) can reuse it.
/// </summary>
public static class LessonPrintViewModelBuilder
{
    public static LessonPrintViewModel Build(Day day, bool publicView = false)
    {
        var metadata = ParseMetadata(day.MetadataJson);

        var bodySections = new List<LessonBodySection>();
        void AddSection(string key, string spanish, string english)
        {
            var md = metadata.BodySections.GetValueOrDefault(key, "");
            if (string.IsNullOrWhiteSpace(md)) return;
            bodySections.Add(new LessonBodySection { Spanish = spanish, English = english, Markdown = md });
        }
        AddSection("apertura",     "Apertura",                       "Opening");
        AddSection("presentacion", "Presentación",                   "Presentation");
        AddSection("extension",    "Extensión para 1.º y 2.º grado", "Extension for 1st & 2nd grade");
        AddSection("actividad",    "Actividad",                      "Activity");
        AddSection("juegos",       "Opciones de Juegos",             "Game Options");
        AddSection("cultura",      "Cultura",                        "Cultural Connection");
        AddSection("cierre",       "Cierre",                         "Closing");

        var standardRows = day.WisconsinStandards
            .OrderBy(s => s.Code)
            .Select(s => new StandardRow
            {
                WiCode = s.Code,
                ActflLabel = WiCodeToActflLabel(s.Code),
                Practice = s.LearnerPractice + " — " + s.LearnerPracticeDescriptor,
                Indicator = s.PerformanceIndicator
            })
            .ToList();

        return new LessonPrintViewModel
        {
            DayId = day.Id,
            Title = day.Title,
            Subtitle = day.Description,
            UnitTitle = day.Unit?.Title ?? "—",
            GradeBandLabel = day.GradeBand.ToString(),
            Sessions = day.Sessions,
            Trimester = metadata.Trimester,
            DurationMinutes = day.EstimatedDurationMinutes,
            Theme = day.Theme,
            Objective = string.IsNullOrWhiteSpace(metadata.Objective) ? day.Description : metadata.Objective,
            VocabCore = metadata.VocabCore,
            VocabStretch = metadata.VocabStretch,
            VocabChallenge = metadata.VocabChallenge,
            Materials = metadata.Materials,
            Videos = day.Videos.OrderBy(v => v.DisplayOrder).ToList(),
            BodySections = bodySections,
            Standards = standardRows,
            PublicView = publicView
        };
    }

    // --- metadata parsing ---

    private sealed class ParsedMetadata
    {
        public List<string> VocabCore { get; set; } = new();
        public List<string> VocabStretch { get; set; } = new();
        public List<string> VocabChallenge { get; set; } = new();
        public List<string> Materials { get; set; } = new();
        public int Trimester { get; set; }
        public string Objective { get; set; } = string.Empty;
        public Dictionary<string, string> BodySections { get; set; } = new();
    }

    private static ParsedMetadata ParseMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ParsedMetadata();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var meta = new ParsedMetadata();
            if (root.TryGetProperty("vocab", out var vocab))
            {
                meta.VocabCore = ReadStringArray(vocab, "core");
                meta.VocabStretch = ReadStringArray(vocab, "stretch");
                meta.VocabChallenge = ReadStringArray(vocab, "challenge");
            }
            meta.Materials = ReadStringArray(root, "materials");
            if (root.TryGetProperty("trimester", out var t) && t.ValueKind == JsonValueKind.Number)
                meta.Trimester = t.GetInt32();
            if (root.TryGetProperty("objective", out var o) && o.ValueKind == JsonValueKind.String)
                meta.Objective = o.GetString() ?? string.Empty;
            if (root.TryGetProperty("bodySections", out var sections))
            {
                foreach (var prop in sections.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        meta.BodySections[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }
            return meta;
        }
        catch (JsonException)
        {
            return new ParsedMetadata();
        }
    }

    private static List<string> ReadStringArray(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new();
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    /// <summary>
    /// Maps a WI DPI World Languages code (e.g. "WL.IT.1.c.n1") to its
    /// ACTFL equivalent label. WI's code scheme bakes in the ACTFL
    /// communication mode + proficiency level, so the mapping is
    /// deterministic.
    /// </summary>
    public static string WiCodeToActflLabel(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var parts = code.Split('.');
        var mode = parts.Length >= 2 ? parts[1] switch
        {
            "IT" => "Interpretive",
            "IP" => "Interpersonal",
            "PS" => "Presentational",
            "IC" => "Intercultural / Cultures",
            "GC" => "Global Competence",
            _    => "ACTFL"
        } : "ACTFL";
        var level = parts.Length >= 5 ? parts[4] switch
        {
            "n1" => "Novice Low",
            "n2" => "Novice Mid",
            "n3" => "Novice High",
            "n+" => "Novice",
            "i1" => "Intermediate Low",
            "i2" => "Intermediate Mid",
            "i3" => "Intermediate High",
            "a1" => "Advanced Low",
            "a2" => "Advanced Mid",
            "a3" => "Advanced High",
            _    => ""
        } : "";
        return string.IsNullOrEmpty(level) ? $"ACTFL {mode}" : $"ACTFL {mode} · {level}";
    }
}
