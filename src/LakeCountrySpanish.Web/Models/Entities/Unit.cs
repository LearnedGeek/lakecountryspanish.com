namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// A thematic grouping of Days within a LearningPath (e.g. "Mundo de Colores",
/// "La Familia", "Animales del Bosque"). Modeled on the Somos curriculum
/// resource-map pattern: each Unit has Core Vocabulary, a Cultural Connection,
/// optional Summative Assessment, and minimum duration. The spiraled-curriculum
/// pattern is supported via <see cref="PriorUnits"/> — units can declare which
/// earlier units they assume vocabulary familiarity with.
/// </summary>
public class Unit
{
    public int Id { get; set; }

    /// <summary>
    /// Display ordering within the parent LearningPath (Unit 1, Unit 2, ...).
    /// </summary>
    public int UnitNumber { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// FK to the parent LearningPath. A Unit belongs to exactly one path.
    /// </summary>
    public int LearningPathId { get; set; }
    public virtual LearningPath LearningPath { get; set; } = null!;

    /// <summary>
    /// Thematic label (e.g. "Colors", "Family", "Animals-Jungle"). Stored as
    /// a free-form string so Karen can introduce new themes without schema
    /// changes.
    /// </summary>
    public string Theme { get; set; } = string.Empty;

    /// <summary>
    /// WI proficiency sub-level this Unit moves students into upon completion.
    /// Per the K4-8 fluency progression table.
    /// </summary>
    public ProficiencySubLevel TargetProficiencySubLevel { get; set; }

    /// <summary>
    /// Core vocabulary introduced in this Unit. Comma- or newline-separated
    /// list of Spanish words/phrases. Spirals into subsequent Units —
    /// teachers can use <see cref="PriorUnits"/> to declare which earlier
    /// units must have been taught first.
    /// </summary>
    public string CoreVocabulary { get; set; } = string.Empty;

    /// <summary>
    /// Named cultural tie-in for this Unit (e.g. "Peruvian flag colors",
    /// "Día de Muertos altar", "Inti Raymi celebration"). First-class field
    /// per Somos pattern; "None" is a valid value when no tie-in fits.
    /// </summary>
    public string CulturalConnection { get; set; } = string.Empty;

    /// <summary>
    /// Estimated minimum number of class periods this Unit takes to teach.
    /// </summary>
    public int MinimumDurationDays { get; set; }

    /// <summary>
    /// Display order within the LearningPath; usually matches UnitNumber but
    /// kept as a separate field to allow reordering without renumbering.
    /// </summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Day> Days { get; set; } = new List<Day>();

    /// <summary>
    /// Self-referencing many-to-many: which earlier Units does this one
    /// assume vocabulary/concept familiarity with? Used to surface
    /// prerequisite warnings to teachers ("Day 12 assumes vocab from Unit 3").
    /// </summary>
    public virtual ICollection<Unit> PriorUnits { get; set; } = new List<Unit>();
    public virtual ICollection<Unit> DependentUnits { get; set; } = new List<Unit>();
}
