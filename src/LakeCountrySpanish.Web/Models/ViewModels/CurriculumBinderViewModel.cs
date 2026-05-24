using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// One rendered binder ready for browser-print. Stitches a lesson plan plus
/// each referenced artifact into a single printable HTML document.
/// </summary>
public sealed class CurriculumBinderViewModel
{
    public RenderedDocument Lesson { get; init; } = null!;
    public IReadOnlyList<RenderedDocument> Artifacts { get; init; } = Array.Empty<RenderedDocument>();
    public string TeacherName { get; init; } = "Teacher";
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    public string ThemeName => Lesson.ThemeName;
}
