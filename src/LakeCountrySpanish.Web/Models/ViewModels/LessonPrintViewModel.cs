using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// Everything the teacher-binder print view needs. Pulled out of a Day +
/// its MetadataJson + LessonVideos + WisconsinStandards. Matches the
/// structure of the hand-converted proposals in
/// docs/lcs-business/curriculum/proposals/ so the on-page render and
/// downloadable PDF read identically.
/// </summary>
public sealed class LessonPrintViewModel
{
    public int DayId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string UnitTitle { get; init; } = string.Empty;
    public string GradeBandLabel { get; init; } = string.Empty;
    public int Sessions { get; init; }
    public int Trimester { get; init; }
    public int DurationMinutes { get; init; }
    public string Theme { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;

    public List<string> VocabCore { get; init; } = new();
    public List<string> VocabStretch { get; init; } = new();
    public List<string> VocabChallenge { get; init; } = new();

    public List<string> Materials { get; init; } = new();

    public List<LessonVideo> Videos { get; init; } = new();

    public List<LessonBodySection> BodySections { get; init; } = new();

    public List<StandardRow> Standards { get; init; } = new();

    /// <summary>When true, this is a member-facing print render (no admin chrome).</summary>
    public bool PublicView { get; init; }
}

public sealed class LessonBodySection
{
    public string Spanish { get; init; } = string.Empty;
    public string English { get; init; } = string.Empty;
    /// <summary>Markdown source for the section body.</summary>
    public string Markdown { get; init; } = string.Empty;
}

public sealed class StandardRow
{
    public string WiCode { get; init; } = string.Empty;
    public string ActflLabel { get; init; } = string.Empty;
    public string Practice { get; init; } = string.Empty;
    public string Indicator { get; init; } = string.Empty;
}
