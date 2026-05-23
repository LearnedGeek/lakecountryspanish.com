namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Level of binder print rights a teacher holds for a class assignment.
/// </summary>
public enum PrintRights
{
    /// <summary>Teacher can browse curriculum but cannot generate binders.</summary>
    None = 0,
    /// <summary>Per-lesson print rights — teacher can print individual Days.</summary>
    PerLesson = 1,
    /// <summary>Per-unit print rights — teacher can print whole Units.</summary>
    PerUnit = 2,
    /// <summary>Full-path print rights — teacher can print the entire LearningPath.</summary>
    FullPath = 3
}

/// <summary>
/// Assigns a teacher to teach a specific LearningPath during a specific Period
/// with specified print rights. Watermarking on generated binders draws from
/// this record (teacher name + period name) so content is traceable to its
/// licensed use.
///
/// When the period ends, print rights expire; the teacher retains browse
/// access but cannot generate new binders without a renewed assignment.
/// </summary>
public class TeacherClassAssignment
{
    public int Id { get; set; }

    /// <summary>
    /// FK to <see cref="ApplicationUser"/> with the Teacher role.
    /// </summary>
    public string TeacherId { get; set; } = string.Empty;
    public virtual ApplicationUser Teacher { get; set; } = null!;

    public int PeriodId { get; set; }
    public virtual Period Period { get; set; } = null!;

    public int LearningPathId { get; set; }
    public virtual LearningPath LearningPath { get; set; } = null!;

    /// <summary>
    /// Grade this assignment covers. Usually matches the LearningPath's
    /// GradeBand but kept explicit to allow off-grade assignments
    /// (e.g. a teacher running a mixed-age small group).
    /// </summary>
    public GradeBand GradeBand { get; set; }

    public PrintRights PrintRights { get; set; } = PrintRights.PerLesson;

    /// <summary>
    /// Effective start of the assignment. Usually matches the Period's
    /// StartDate; stored separately to support mid-period reassignment.
    /// </summary>
    public DateTime EffectiveStart { get; set; }

    /// <summary>
    /// Effective end of the assignment. Usually matches the Period's EndDate.
    /// </summary>
    public DateTime EffectiveEnd { get; set; }

    /// <summary>
    /// Optional class display name (e.g. "Wednesday K4 Spanish" or "St. Mary's
    /// after-school group").
    /// </summary>
    public string? ClassName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<BinderComposition> BinderCompositions { get; set; } = new List<BinderComposition>();
    public virtual ICollection<BinderGeneration> BinderGenerations { get; set; } = new List<BinderGeneration>();
}
