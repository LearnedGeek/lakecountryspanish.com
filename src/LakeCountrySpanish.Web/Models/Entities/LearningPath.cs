namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Intended audience for a learning path — informs marketing, scheduling
/// expectations, and content tone.
/// </summary>
public enum PathAudience
{
    Classroom = 0,
    HomeSchool = 1,
    AfterSchool = 2,
    Tutoring1on1 = 3,
    HeritageSpeaker = 4,
    Online = 5,
    SummerCamp = 6
}

/// <summary>
/// A complete sequence of Units for one grade and audience. Per the LCS
/// "fluency readiness" thesis, every LearningPath commits to a specific
/// proficiency endpoint that students reach upon completing it. The K4 path
/// targets Novice Low; the 5th grade path targets Intermediate Low; etc.
/// </summary>
public class LearningPath
{
    public int Id { get; set; }

    /// <summary>
    /// Display title, e.g. "K4 Spanish — Foundations" or "5th Grade —
    /// Conversational Builder".
    /// </summary>
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Single grade this path targets. K4-8 are distinct paths; per-grade
    /// differentiation is the LCS product promise.
    /// </summary>
    public GradeBand GradeBand { get; set; }

    /// <summary>
    /// The WI proficiency band students reach upon completing this path
    /// (e.g. Novice for K4-2, Intermediate for grades 4-8).
    /// </summary>
    public ProficiencyBand TargetProficiencyBand { get; set; }

    /// <summary>
    /// The WI sub-level students reach upon completing this path. Refer to the
    /// K4-8 fluency progression table in <c>docs/curriculum-system/PLAN.md</c>.
    /// </summary>
    public ProficiencySubLevel TargetProficiencySubLevel { get; set; }

    /// <summary>
    /// Intended audience (classroom, home-school, after-school, etc.).
    /// </summary>
    public PathAudience Audience { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}
