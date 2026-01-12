using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Request for generating an assignment via Claude API.
/// </summary>
public class AssignmentGenerationRequest
{
    public string CefrLevel { get; set; } = "A1";
    public AssignmentType Type { get; set; }
    public string? TopicName { get; set; }
    public string? TopicDescription { get; set; }
    public string? Keywords { get; set; }
    public int QuestionCount { get; set; } = 5;
    public string? AdditionalInstructions { get; set; }
}

/// <summary>
/// Response from Claude API for assignment generation.
/// </summary>
public class GeneratedAssignment
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string QuestionsJson { get; set; } = "[]";
    public string AnswersJson { get; set; } = "[]";
    public int TotalPoints { get; set; }
    public int EstimatedMinutes { get; set; }
    public string? GenerationPrompt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request for grading an open-ended response.
/// </summary>
public class GradingRequest
{
    public string Question { get; set; } = string.Empty;
    public string StudentAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? AcceptableVariations { get; set; }
    public string CefrLevel { get; set; } = "A1";
}

/// <summary>
/// Response from Claude API for grading.
/// </summary>
public class GradingResult
{
    public bool IsCorrect { get; set; }
    public decimal Score { get; set; } // 0.0 to 1.0
    public string Feedback { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public string? SuggestedImprovement { get; set; }
}

/// <summary>
/// Service for interacting with Claude API for assignment generation and grading.
/// </summary>
public interface IClaudeApiService
{
    /// <summary>
    /// Generate an assignment using Claude API.
    /// </summary>
    Task<GeneratedAssignment> GenerateAssignmentAsync(AssignmentGenerationRequest request);

    /// <summary>
    /// Grade an open-ended student response using Claude API.
    /// </summary>
    Task<GradingResult> GradeResponseAsync(GradingRequest request);

    /// <summary>
    /// Check if the Claude API is configured and available.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Generate a single placement test question for the specified CEFR level.
    /// </summary>
    Task<PlacementQuestion> GeneratePlacementQuestionAsync(
        string cefrLevel,
        List<string>? previousTopics = null);
}
