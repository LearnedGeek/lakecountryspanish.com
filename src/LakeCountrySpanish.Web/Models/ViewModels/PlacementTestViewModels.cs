namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// View model for the placement test introduction page.
/// </summary>
public class PlacementTestIndexViewModel
{
    public bool HasActiveSession { get; set; }
    public int? ActiveSessionId { get; set; }
    public bool HasCompletedTest { get; set; }
    public string? CurrentLevel { get; set; }
    public DateTime? LastTestDate { get; set; }
}

/// <summary>
/// View model for displaying a placement test question.
/// </summary>
public class PlacementTestQuestionViewModel
{
    public int SessionId { get; set; }
    public int QuestionNumber { get; set; }
    public string CurrentLevel { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
    public string? Hint { get; set; }
    public int TotalAnswered { get; set; }
    public int MaxQuestions { get; set; } = 30;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int ProgressPercentage => MaxQuestions > 0
        ? Math.Min(100, (int)((double)TotalAnswered / MaxQuestions * 100))
        : 0;
}

/// <summary>
/// View model for submitting an answer.
/// </summary>
public class PlacementTestSubmitViewModel
{
    public int SessionId { get; set; }
    public string Answer { get; set; } = string.Empty;
}

/// <summary>
/// View model for answer feedback (shown briefly before next question).
/// </summary>
public class PlacementTestAnswerFeedbackViewModel
{
    public int SessionId { get; set; }
    public bool IsCorrect { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public bool TestComplete { get; set; }
    public string? DeterminedLevel { get; set; }
}

/// <summary>
/// View model for placement test results.
/// </summary>
public class PlacementTestResultViewModel
{
    public int SessionId { get; set; }
    public string DeterminedLevel { get; set; } = string.Empty;
    public string LevelDescription { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int TotalCorrect { get; set; }
    public decimal OverallPercentage => TotalQuestions > 0
        ? Math.Round((decimal)TotalCorrect / TotalQuestions * 100, 1)
        : 0;
    public TimeSpan TimeTaken { get; set; }
    public Dictionary<string, LevelPerformance> LevelBreakdown { get; set; } = new();

    public string GetLevelDescription(string level) => level switch
    {
        "A1" => "Beginner - You can understand and use basic everyday expressions and simple phrases.",
        "A2" => "Elementary - You can communicate in simple, routine tasks and describe your background.",
        "B1" => "Intermediate - You can deal with most situations while traveling and describe experiences and events.",
        "B2" => "Upper Intermediate - You can interact with fluency and spontaneity with native speakers.",
        "C1" => "Advanced - You can express ideas fluently and use language flexibly for social and professional purposes.",
        "C2" => "Mastery - You can understand virtually everything and express yourself spontaneously with precision.",
        _ => "Your Spanish proficiency level has been determined."
    };
}

/// <summary>
/// Performance stats for a single CEFR level.
/// </summary>
public class LevelPerformance
{
    public int Correct { get; set; }
    public int Total { get; set; }
    public decimal Percentage => Total > 0
        ? Math.Round((decimal)Correct / Total * 100, 1)
        : 0;
}

/// <summary>
/// Represents a generated placement question from the AI.
/// </summary>
public class PlacementQuestion
{
    public string Level { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "multiple_choice", "fill_blank", "translation"
    public string Question { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public List<string>? AcceptableAnswers { get; set; }
    public string? Hint { get; set; }
    public string? Explanation { get; set; }
    public string? Topic { get; set; } // grammar, vocabulary, etc.
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
