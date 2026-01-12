namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Type of curriculum topic.
/// </summary>
public enum TopicType
{
    Grammar,
    Vocabulary,
    Reading,
    Listening,
    Speaking,
    Writing,
    Culture
}

/// <summary>
/// Represents a curriculum topic for a specific CEFR level.
/// </summary>
public class CurriculumTopic
{
    public int Id { get; set; }

    /// <summary>
    /// Topic name (e.g., "Present Tense Conjugation", "Food Vocabulary")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the topic
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// CEFR level this topic belongs to (A1, A2, B1, B2, C1, C2)
    /// </summary>
    public string CefrLevel { get; set; } = "A1";

    /// <summary>
    /// Type of topic (Grammar, Vocabulary, etc.)
    /// </summary>
    public TopicType Type { get; set; }

    /// <summary>
    /// Order in which topics should be presented within a level
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether this topic is available for assignment generation
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Additional keywords for AI context when generating assignments
    /// </summary>
    public string? Keywords { get; set; }

    /// <summary>
    /// Example sentences or content for AI reference
    /// </summary>
    public string? ExampleContent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
