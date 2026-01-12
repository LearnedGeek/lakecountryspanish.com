namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Student's difficulty feedback for an assignment.
/// </summary>
public enum DifficultyFeedback
{
    TooEasy,
    JustRight,
    Challenging,
    TooHard
}

/// <summary>
/// Represents a student's submission/completion of an assignment.
/// </summary>
public class AssignmentSubmission
{
    public int Id { get; set; }

    /// <summary>
    /// The assignment that was submitted
    /// </summary>
    public int AssignmentId { get; set; }

    /// <summary>
    /// The student who submitted
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// JSON containing the student's answers
    /// [{ "id": 1, "answer": "..." }]
    /// </summary>
    public string AnswersJson { get; set; } = "[]";

    /// <summary>
    /// JSON containing the grading results
    /// [{ "id": 1, "isCorrect": true, "feedback": "...", "correctAnswer": "..." }]
    /// </summary>
    public string GradingResultJson { get; set; } = "[]";

    /// <summary>
    /// Number of correct answers
    /// </summary>
    public int CorrectAnswers { get; set; }

    /// <summary>
    /// Total number of questions
    /// </summary>
    public int TotalQuestions { get; set; }

    /// <summary>
    /// Score as percentage (0-100)
    /// </summary>
    public decimal PercentageScore { get; set; }

    /// <summary>
    /// Base points earned from correct answers
    /// </summary>
    public int PointsEarned { get; set; }

    /// <summary>
    /// Bonus points earned (e.g., for perfect score)
    /// </summary>
    public int BonusPointsEarned { get; set; }

    /// <summary>
    /// Total points earned (base + bonus)
    /// </summary>
    public int TotalPointsEarned => PointsEarned + BonusPointsEarned;

    /// <summary>
    /// Time taken to complete in seconds
    /// </summary>
    public int TimeTakenSeconds { get; set; }

    /// <summary>
    /// Student's feedback on difficulty
    /// </summary>
    public DifficultyFeedback? DifficultyFeedback { get; set; }

    /// <summary>
    /// Optional notes from the student
    /// </summary>
    public string? StudentNotes { get; set; }

    /// <summary>
    /// Whether this was the first attempt at this assignment
    /// </summary>
    public bool IsFirstAttempt { get; set; } = true;

    /// <summary>
    /// Attempt number (1, 2, 3...)
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// When the student started the assignment
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the student submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Assignment Assignment { get; set; } = null!;
    public virtual ApplicationUser Student { get; set; } = null!;
}
