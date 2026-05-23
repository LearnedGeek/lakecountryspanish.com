namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// A teacher's ad-hoc selection of curriculum content to print as a binder
/// for a specific class assignment. References Days from the assigned
/// LearningPath plus any extra ArtifactLibrary items the teacher pulled in
/// from the broader library beyond what each Day recommends.
///
/// Teachers can save a composition as a reusable template via
/// <see cref="IsTemplate"/> — useful for "I always include these warmups"
/// patterns.
/// </summary>
public class BinderComposition
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// FK to the TeacherClassAssignment that authorizes this binder's
    /// content. Watermarking on the generated binder uses the assignment's
    /// teacher + period.
    /// </summary>
    public int TeacherClassAssignmentId { get; set; }
    public virtual TeacherClassAssignment TeacherClassAssignment { get; set; } = null!;

    /// <summary>
    /// Whether this composition is saved as a reusable template (not a
    /// specific binder for a specific week).
    /// </summary>
    public bool IsTemplate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Days included in this binder, in the order the teacher chose.
    /// </summary>
    public virtual ICollection<Day> Days { get; set; } = new List<Day>();

    /// <summary>
    /// Extra ArtifactLibrary items the teacher pulled in beyond what each
    /// Day recommends.
    /// </summary>
    public virtual ICollection<ArtifactLibrary> ExtraArtifacts { get; set; } = new List<ArtifactLibrary>();
}
