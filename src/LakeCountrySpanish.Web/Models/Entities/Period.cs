namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// A teaching period (semester, quarter, session) used to scope
/// <see cref="TeacherClassAssignment"/> records and binder watermarking.
/// Periods give the LLC time-bounded "rental" of curriculum to teachers.
/// </summary>
public class Period
{
    public int Id { get; set; }

    /// <summary>
    /// Display name. e.g. "Fall 2026", "Spring 2027", "Summer Camp 2026".
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Whether this period is currently active. Used to filter selectable
    /// periods in the teacher assignment UI.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<TeacherClassAssignment> TeacherClassAssignments { get; set; } = new List<TeacherClassAssignment>();
}
