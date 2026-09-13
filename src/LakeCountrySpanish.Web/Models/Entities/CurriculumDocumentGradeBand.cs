namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Join row associating a <see cref="CurriculumDocument"/> with a single
/// <see cref="GradeBand"/>. A single document typically has multiple
/// rows — one per grade band it covers (e.g. K-2 binder covers K4, K5,
/// Grade1, Grade2 = 4 rows). Cascade-deleted with the document.
/// </summary>
public class CurriculumDocumentGradeBand
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public virtual CurriculumDocument Document { get; set; } = null!;

    public GradeBand GradeBand { get; set; }
}
