namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// LCS-internal grade band assignment. Each grade is distinct — the K4-8 product
/// commits to per-grade differentiation rather than wide bands. WI standards
/// themselves are proficiency-banded (not grade-banded); this enum is purely an
/// LCS editorial layer.
/// </summary>
public enum GradeBand
{
    K4 = 0,
    K5 = 1,
    Grade1 = 2,
    Grade2 = 3,
    Grade3 = 4,
    Grade4 = 5,
    Grade5 = 6,
    Grade6 = 7,
    Grade7 = 8,
    Grade8 = 9
}

/// <summary>
/// Pedagogical skill focus for a lesson or artifact. Independent of WI standards
/// (which integrate listening/reading and speaking/writing into the Interpretive
/// and Presentational modes) — kept here because the distinction is useful when
/// Karen wants to tag content by skill mode.
/// </summary>
public enum SkillFocus
{
    Mixed = 0,
    Listening = 1,
    Reading = 2,
    Speaking = 3,
    Writing = 4
}
