using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// Public /programs page — programs grouped by enrollment state so parents
/// see actionable offerings first, then upcoming-but-closed, then a history
/// trail of past programs (for social proof / next-year recruiting).
/// </summary>
public sealed class ProgramsCalendarViewModel
{
    /// <summary>Programs a parent can enroll in right now (window is open).</summary>
    public IReadOnlyList<EnrollmentProgram> EnrollmentOpen { get; init; } = Array.Empty<EnrollmentProgram>();

    /// <summary>
    /// Programs whose enrollment window hasn't opened yet (EnrollmentStartsAt
    /// is in the future). Shown right below Enrolling Now so parents can see
    /// what's on the horizon and note the opening date.
    /// </summary>
    public IReadOnlyList<EnrollmentProgram> ComingSoon { get; init; } = Array.Empty<EnrollmentProgram>();

    /// <summary>
    /// Programs whose enrollment window has closed but whose end date hasn't
    /// arrived yet — students already enrolled are still meeting; new signups
    /// aren't accepted. Shown so parents know these programs exist and can
    /// contact us for waitlist / next session interest.
    /// </summary>
    public IReadOnlyList<EnrollmentProgram> ClosedButUpcoming { get; init; } = Array.Empty<EnrollmentProgram>();

    /// <summary>Past programs — kept visible as social proof + so parents can request the next run.</summary>
    public IReadOnlyList<EnrollmentProgram> Past { get; init; } = Array.Empty<EnrollmentProgram>();

    public bool HasAny =>
        EnrollmentOpen.Count > 0
        || ComingSoon.Count > 0
        || ClosedButUpcoming.Count > 0
        || Past.Count > 0;
}
