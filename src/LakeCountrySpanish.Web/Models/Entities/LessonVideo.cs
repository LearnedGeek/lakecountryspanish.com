namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Where the video sits in the lesson flow. Matches the roles Karen uses in
/// her docs (Apertura/Presentación/Cierre) plus a generic Supplemental bucket.
/// </summary>
public enum LessonVideoRole
{
    Opening = 1,         // Apertura — welcome song / saludo
    Presentation = 2,    // Presentación — vocab song
    Closing = 3,         // Cierre — despedida
    Supplemental = 4     // Any extra reference video (commands, culture, etc.)
}

/// <summary>
/// A video referenced by a Day (lesson). One row per video; usually 2-4 per Day.
/// Populated from the Songs / Media table in the uploaded docx template.
///
/// Shortlinks can target either the parent Day (lesson page) or a specific
/// video — see <see cref="Shortlink"/>.
/// </summary>
public class LessonVideo
{
    public int Id { get; set; }

    public int DayId { get; set; }
    public virtual Day Day { get; set; } = null!;

    /// <summary>
    /// Display title — what the teacher sees in the binder ("Buenos días",
    /// "Mi familia"). Free-form from the docx Songs table.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full video URL — typically YouTube but kept open so Vimeo / direct MP4
    /// links also work.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    public LessonVideoRole Role { get; set; } = LessonVideoRole.Supplemental;

    /// <summary>
    /// Display order within the lesson's video list. Lower numbers first.
    /// </summary>
    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
