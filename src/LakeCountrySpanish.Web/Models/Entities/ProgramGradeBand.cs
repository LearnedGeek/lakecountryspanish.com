namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Join row associating an <see cref="EnrollmentProgram"/> with a single
/// <see cref="GradeBand"/>. A program with <see cref="AudienceType.Grades"/>
/// typically has multiple of these — one per eligible grade in a
/// contiguous range (e.g. K–2 = K4, K5, Grade1, Grade2). Non-contiguous
/// sets are allowed (e.g. Grade3, Grade5, Grade7).
///
/// Cascade-deleted with the parent Program.
/// </summary>
public class ProgramGradeBand
{
    public int Id { get; set; }

    public int ProgramId { get; set; }
    public virtual EnrollmentProgram Program { get; set; } = null!;

    public GradeBand GradeBand { get; set; }
}
