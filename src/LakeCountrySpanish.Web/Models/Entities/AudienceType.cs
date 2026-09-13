namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Discriminated audience label for an <see cref="EnrollmentProgram"/>.
/// Every program picks exactly one — the field is never null. Prior to
/// 2026-09-13 this concept was overloaded onto a free-form
/// <c>GradeRange</c> string (Karen used <c>"0"</c> as a placeholder for
/// "no grade / adult program"), which confused parents ("Grades 0 · ages
/// 18–99") and made structured filtering (e.g. teachers filtering
/// binders by grade band) impossible. See issue #19.
/// </summary>
public enum AudienceType
{
    /// <summary>
    /// Grade-based program. <see cref="EnrollmentProgram.GradeBands"/> is
    /// populated with one or more <see cref="GradeBand"/> values covering
    /// the eligible grades (typically a contiguous range, but non-contiguous
    /// sets are allowed).
    /// </summary>
    Grades = 0,

    /// <summary>
    /// Adult program (18+). Grade concept doesn't apply. Age Min/Max still
    /// carry the actual age eligibility (e.g. 18–99, 25–65).
    /// </summary>
    Adult = 1,

    /// <summary>
    /// No audience restriction — family programs, drop-ins, community events.
    /// Both grade and age are unrestricted at the display layer.
    /// </summary>
    All = 2
}
