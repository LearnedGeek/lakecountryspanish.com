using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services.Curriculum;

/// <summary>
/// Structured result of parsing a lesson template .docx. Maps onto a Day
/// (top-level metadata + body) plus a LessonVideo set (songs) plus an
/// optional set of WI standard codes.
///
/// Body sections are kept as raw text (one entry per Apertura/Presentación/
/// etc.) — the admin step that creates the Day decides how to compile them
/// into the existing BodyBlocksJson structure.
/// </summary>
public sealed class ParsedLesson
{
    // --- Metadata table ---
    public string Title { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string GradeBand { get; set; } = string.Empty;
    public int Sessions { get; set; } = 1;
    public int Trimester { get; set; }
    public string Theme { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 30;

    // --- Vocabulary table (one entry per row) ---
    public List<string> VocabCore { get; } = new();
    public List<string> VocabStretch { get; } = new();
    public List<string> VocabChallenge { get; } = new();

    // --- Materials list ---
    public List<string> Materials { get; } = new();

    // --- Songs table ---
    public List<ParsedSong> Songs { get; } = new();

    // --- Standards table ---
    public List<string> StandardCodes { get; } = new();

    // --- Body sections (Apertura, Presentación, Extensión, Actividad, Juegos, Cultura, Cierre) ---
    public Dictionary<LessonSection, string> BodySections { get; } = new();
}

public sealed class ParsedSong
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public LessonVideoRole Role { get; set; }
    public int DisplayOrder { get; set; }
}
