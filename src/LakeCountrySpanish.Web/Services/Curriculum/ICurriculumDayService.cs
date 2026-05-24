using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services.Curriculum;

/// <summary>
/// Admin-facing service for authoring <see cref="Day"/> entities. Adds a
/// version-snapshot side effect on every save so we can reconstruct a binder
/// exactly as it was generated even after the master content has evolved.
/// </summary>
public interface ICurriculumDayService
{
    Task<IReadOnlyList<Day>> ListAsync(int? unitId = null, GradeBand? gradeBand = null, bool includeInactive = false, CancellationToken ct = default);

    Task<Day?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Persists a new Day. Snapshots a v1 CurriculumVersion in the same
    /// transaction. <paramref name="changeNotes"/> is optional change-log text.
    /// </summary>
    Task<Day> CreateAsync(Day day, string authorUserId, string? changeNotes = null, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing Day. If the TeacherPlanMarkdown changed, snapshots
    /// a new CurriculumVersion row at the next sequential version number.
    /// Pure metadata edits (title, theme, grade band) bump LastModifiedAt
    /// without a new version row.
    /// </summary>
    Task<Day> UpdateAsync(Day day, string editorUserId, string? changeNotes = null, CancellationToken ct = default);
}
