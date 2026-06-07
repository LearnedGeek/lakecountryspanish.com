namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// What a shortlink points at. Lessons gate access through the Student
/// auth wall; videos can also be Lesson-gated by going through the lesson
/// page, but DestinationType lets us also point direct at a video later.
/// </summary>
public enum ShortlinkDestination
{
    Lesson = 1,    // → /curriculum/{slug}
    Video = 2      // → the video URL (or the lesson + video anchor)
}

/// <summary>
/// 3-character alphanumeric short codes that resolve to a lesson page (or
/// later, a specific video). `lcs/AB12` style links Karen wants teachers to
/// type in the classroom.
///
/// Code space: 36^3 = 46,656 codes (uppercase A-Z + digits 0-9).
/// </summary>
public class Shortlink
{
    public int Id { get; set; }

    /// <summary>
    /// 3-character alphanumeric code (uppercase letters + digits). Unique.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public ShortlinkDestination DestinationType { get; set; } = ShortlinkDestination.Lesson;

    /// <summary>
    /// FK into the table indicated by DestinationType.
    /// For Lesson → Day.Id. For Video → LessonVideo.Id.
    /// </summary>
    public int DestinationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Click counter — light-touch analytics. Incremented on each redirect;
    /// no hot-path cost (single UPDATE per hit).
    /// </summary>
    public int ClickCount { get; set; } = 0;

    public DateTime? LastClickedAt { get; set; }
}
