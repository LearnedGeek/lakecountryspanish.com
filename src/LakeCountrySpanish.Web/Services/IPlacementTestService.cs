using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service interface for managing CEFR placement tests.
/// </summary>
public interface IPlacementTestService
{
    /// <summary>
    /// Start a new placement test for a student.
    /// </summary>
    Task<PlacementTestSession> StartTestAsync(string studentId);

    /// <summary>
    /// Get the next question for an active test session.
    /// </summary>
    Task<PlacementTestQuestionViewModel?> GetNextQuestionAsync(int sessionId);

    /// <summary>
    /// Submit an answer and determine if the test should continue.
    /// </summary>
    Task<PlacementTestAnswerFeedbackViewModel> SubmitAnswerAsync(int sessionId, string answer);

    /// <summary>
    /// Get the results for a completed test.
    /// </summary>
    Task<PlacementTestResultViewModel?> GetResultsAsync(int sessionId);

    /// <summary>
    /// Get an active (in-progress) test session for a student.
    /// </summary>
    Task<PlacementTestSession?> GetActiveSessionAsync(string studentId);

    /// <summary>
    /// Get a test session by ID.
    /// </summary>
    Task<PlacementTestSession?> GetSessionByIdAsync(int sessionId);

    /// <summary>
    /// Abandon an in-progress test session.
    /// </summary>
    Task<bool> AbandonTestAsync(int sessionId);

    /// <summary>
    /// Check if a student has completed a placement test.
    /// </summary>
    Task<bool> HasCompletedTestAsync(string studentId);

    /// <summary>
    /// Get the most recent completed test for a student.
    /// </summary>
    Task<PlacementTestSession?> GetLastCompletedTestAsync(string studentId);
}
