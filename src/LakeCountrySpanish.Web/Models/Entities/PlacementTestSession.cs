namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Status of a placement test session.
/// </summary>
public enum PlacementTestStatus
{
    InProgress,
    Completed,
    Abandoned
}

/// <summary>
/// Represents a student's placement test session to determine their CEFR level.
/// </summary>
public class PlacementTestSession
{
    public int Id { get; set; }

    /// <summary>
    /// The student taking the test
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the test session
    /// </summary>
    public PlacementTestStatus Status { get; set; } = PlacementTestStatus.InProgress;

    /// <summary>
    /// The level where the test started (typically A2)
    /// </summary>
    public string StartingLevel { get; set; } = "A2";

    /// <summary>
    /// The current level being tested
    /// </summary>
    public string CurrentLevel { get; set; } = "A2";

    /// <summary>
    /// The final determined level (set when test completes)
    /// </summary>
    public string? DeterminedLevel { get; set; }

    /// <summary>
    /// JSON array of all questions and answers
    /// [{ "level": "A2", "question": "...", "answer": "...", "correct": true, "correctAnswer": "..." }]
    /// </summary>
    public string QuestionsJson { get; set; } = "[]";

    /// <summary>
    /// JSON object tracking progress per level
    /// { "A1": { "correct": 3, "total": 5 }, "A2": { "correct": 4, "total": 5 } }
    /// </summary>
    public string LevelProgressJson { get; set; } = "{}";

    /// <summary>
    /// Total questions answered so far
    /// </summary>
    public int TotalQuestionsAnswered { get; set; }

    /// <summary>
    /// Total correct answers so far
    /// </summary>
    public int TotalCorrect { get; set; }

    /// <summary>
    /// Number of times the student failed to advance (used for determining when to stop)
    /// </summary>
    public int FailedAdvanceCount { get; set; }

    /// <summary>
    /// When the test was started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the test was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
}
