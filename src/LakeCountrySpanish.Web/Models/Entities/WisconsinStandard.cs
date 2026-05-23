namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// One of Wisconsin's five World Languages standards, per the 2019 WI DPI
/// publication. Communication standards 1-3 cover spoken, written, or signed
/// language in three modes. Cultural standards 4-5 cover intercultural
/// competence and global engagement.
/// </summary>
public enum WisconsinStandardCategory
{
    Interpretive = 1,
    Interpersonal = 2,
    Presentational = 3,
    Intercultural = 4,
    GlobalCompetence = 5
}

/// <summary>
/// ACTFL proficiency band per the WI standards framework. WI publishes one
/// standard with three sub-levels per band (n1/n2/n3 for Novice).
/// </summary>
public enum ProficiencyBand
{
    Novice = 1,
    Intermediate = 2,
    Advanced = 3
}

/// <summary>
/// Sub-level within a proficiency band (Low / Mid / High). For WI standards 4
/// and 5, only a single Novice indicator is published with no sub-band; we
/// model that as <see cref="Unspecified"/>.
/// </summary>
public enum ProficiencySubLevel
{
    Unspecified = 0,
    Low = 1,
    Mid = 2,
    High = 3
}

/// <summary>
/// A single Wisconsin DPI World Languages standard performance indicator at a
/// specific proficiency sub-level. Standards are proficiency-banded (not
/// grade-banded) in the WI framework; LCS layers grade-band applicability on
/// top via the <see cref="ApplicableToK4_2"/> flag and editorial tagging on
/// individual Days and ArtifactLibrary items.
/// </summary>
public class WisconsinStandard
{
    public int Id { get; set; }

    /// <summary>
    /// Full WI standard code, e.g. "WL.IT.1.a.n1". Format:
    /// WL.{Category}.{StandardNumber}.{LearnerPractice}.{ProficiencyLevel}.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// One of the five WI standards (Interpretive / Interpersonal / Presentational
    /// / Intercultural / GlobalCompetence).
    /// </summary>
    public WisconsinStandardCategory Category { get; set; }

    /// <summary>
    /// Standard number 1-5 mirroring <see cref="Category"/>. Stored explicitly
    /// for sorting and display.
    /// </summary>
    public int StandardNumber { get; set; }

    /// <summary>
    /// Learner practice identifier within the standard, e.g. "1.a", "2.b".
    /// </summary>
    public string LearnerPractice { get; set; } = string.Empty;

    /// <summary>
    /// Proficiency band (Novice / Intermediate / Advanced). K4-2 learners
    /// operate in Novice across all assessable standards.
    /// </summary>
    public ProficiencyBand ProficiencyBand { get; set; }

    /// <summary>
    /// Sub-level within the proficiency band (Low / Mid / High). Set to
    /// <see cref="ProficiencySubLevel.Unspecified"/> for standards 4 and 5,
    /// which publish only one Novice indicator (n+) with no sub-bands.
    /// </summary>
    public ProficiencySubLevel ProficiencySubLevel { get; set; }

    /// <summary>
    /// Short descriptor of the learner practice (what the practice IS).
    /// </summary>
    public string LearnerPracticeDescriptor { get; set; } = string.Empty;

    /// <summary>
    /// Verbatim performance indicator from the WI document (what the student
    /// does at this level).
    /// </summary>
    public string PerformanceIndicator { get; set; } = string.Empty;

    /// <summary>
    /// Source citation, e.g. "WI DPI Wisconsin Standards for World Languages,
    /// June 2019".
    /// </summary>
    public string SourceDocument { get; set; } = string.Empty;

    /// <summary>
    /// Page number in the source document for traceability.
    /// </summary>
    public int? SourcePage { get; set; }

    /// <summary>
    /// Whether this standard is appropriate to tag against K4-2 content.
    /// Defaults true for standards 1-4; false for standard 5 (Global Competence
    /// indicators are written for older learners with research-based tasks).
    /// </summary>
    public bool ApplicableToK4_2 { get; set; } = true;

    /// <summary>
    /// Effective date of the standards document this entry is drawn from.
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
