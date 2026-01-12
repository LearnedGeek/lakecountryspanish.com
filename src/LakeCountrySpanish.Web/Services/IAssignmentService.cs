using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Result of submitting an assignment.
/// </summary>
public class SubmissionResult
{
    public AssignmentSubmission Submission { get; set; } = null!;
    public int PointsEarned { get; set; }
    public int BonusPointsEarned { get; set; }
    public bool IsPerfectScore { get; set; }
    public bool IsFirstAttempt { get; set; }
    public IEnumerable<QuestionResult> QuestionResults { get; set; } = new List<QuestionResult>();
}

/// <summary>
/// Result for a single question in a submission.
/// </summary>
public class QuestionResult
{
    public int QuestionId { get; set; }
    public bool IsCorrect { get; set; }
    public string StudentAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Feedback { get; set; }
    public string? Explanation { get; set; }
}

/// <summary>
/// Service interface for managing assignments and submissions.
/// </summary>
public interface IAssignmentService
{
    // Curriculum Topics

    /// <summary>
    /// Get all curriculum topics.
    /// </summary>
    Task<IEnumerable<CurriculumTopic>> GetAllTopicsAsync();

    /// <summary>
    /// Get topics by CEFR level.
    /// </summary>
    Task<IEnumerable<CurriculumTopic>> GetTopicsByLevelAsync(string cefrLevel);

    /// <summary>
    /// Get a specific topic by ID.
    /// </summary>
    Task<CurriculumTopic?> GetTopicByIdAsync(int topicId);

    /// <summary>
    /// Create a new curriculum topic.
    /// </summary>
    Task<CurriculumTopic> CreateTopicAsync(CurriculumTopic topic);

    /// <summary>
    /// Update a curriculum topic.
    /// </summary>
    Task<CurriculumTopic?> UpdateTopicAsync(CurriculumTopic topic);

    // Assignments

    /// <summary>
    /// Get all assignments with optional filtering.
    /// </summary>
    Task<IEnumerable<Assignment>> GetAssignmentsAsync(
        string? cefrLevel = null,
        AssignmentStatus? status = null,
        AssignmentType? type = null);

    /// <summary>
    /// Get a specific assignment by ID.
    /// </summary>
    Task<Assignment?> GetAssignmentByIdAsync(int assignmentId);

    /// <summary>
    /// Generate a new assignment using Claude API.
    /// </summary>
    Task<Assignment> GenerateAssignmentAsync(
        string cefrLevel,
        AssignmentType type,
        int? topicId = null,
        int questionCount = 5,
        string? additionalInstructions = null,
        string? createdById = null);

    /// <summary>
    /// Create a manual assignment.
    /// </summary>
    Task<Assignment> CreateAssignmentAsync(Assignment assignment);

    /// <summary>
    /// Update an existing assignment.
    /// </summary>
    Task<Assignment?> UpdateAssignmentAsync(Assignment assignment);

    /// <summary>
    /// Approve an assignment for student use.
    /// </summary>
    Task<bool> ApproveAssignmentAsync(int assignmentId, string reviewerId, string? notes = null);

    /// <summary>
    /// Reject an assignment.
    /// </summary>
    Task<bool> RejectAssignmentAsync(int assignmentId, string reviewerId, string? notes = null);

    /// <summary>
    /// Archive an assignment.
    /// </summary>
    Task<bool> ArchiveAssignmentAsync(int assignmentId);

    // Content Library

    /// <summary>
    /// Find matching approved assignments in the content library.
    /// </summary>
    Task<IEnumerable<Assignment>> FindLibraryMatchesAsync(
        string cefrLevel,
        AssignmentType type,
        int? topicId = null,
        int maxResults = 5);

    /// <summary>
    /// Clone an existing assignment from the content library.
    /// </summary>
    Task<Assignment> CloneFromLibraryAsync(
        int sourceAssignmentId,
        string? createdById = null);

    // Student Assignments

    /// <summary>
    /// Get assignments for a student.
    /// </summary>
    Task<IEnumerable<StudentAssignment>> GetStudentAssignmentsAsync(
        string studentId,
        StudentAssignmentStatus? status = null);

    /// <summary>
    /// Assign an assignment to a student.
    /// </summary>
    Task<StudentAssignment> AssignToStudentAsync(
        string studentId,
        int assignmentId,
        string? assignedById = null,
        DateTime? dueDate = null,
        string? notes = null,
        int priority = 0);

    /// <summary>
    /// Assign an assignment to multiple students.
    /// </summary>
    Task<IEnumerable<StudentAssignment>> AssignToStudentsAsync(
        IEnumerable<string> studentIds,
        int assignmentId,
        string? assignedById = null,
        DateTime? dueDate = null);

    /// <summary>
    /// Get a student assignment by ID.
    /// </summary>
    Task<StudentAssignment?> GetStudentAssignmentAsync(int studentAssignmentId);

    /// <summary>
    /// Mark a student assignment as started.
    /// </summary>
    Task<StudentAssignment?> StartAssignmentAsync(int studentAssignmentId);

    /// <summary>
    /// Skip a student assignment.
    /// </summary>
    Task<bool> SkipAssignmentAsync(int studentAssignmentId);

    // Submissions

    /// <summary>
    /// Submit answers for an assignment.
    /// </summary>
    Task<SubmissionResult> SubmitAssignmentAsync(
        string studentId,
        int assignmentId,
        string answersJson,
        int timeTakenSeconds,
        DifficultyFeedback? difficultyFeedback = null,
        string? studentNotes = null);

    /// <summary>
    /// Get submissions for a student.
    /// </summary>
    Task<IEnumerable<AssignmentSubmission>> GetStudentSubmissionsAsync(
        string studentId,
        int? limit = null);

    /// <summary>
    /// Get submissions for an assignment.
    /// </summary>
    Task<IEnumerable<AssignmentSubmission>> GetAssignmentSubmissionsAsync(int assignmentId);

    /// <summary>
    /// Get a specific submission by ID.
    /// </summary>
    Task<AssignmentSubmission?> GetSubmissionByIdAsync(int submissionId);

    // Statistics

    /// <summary>
    /// Get assignment statistics for a student.
    /// </summary>
    Task<StudentAssignmentStats> GetStudentStatsAsync(string studentId);

    /// <summary>
    /// Get overall assignment statistics.
    /// </summary>
    Task<AssignmentStats> GetOverallStatsAsync();
}

/// <summary>
/// Statistics for a student's assignments.
/// </summary>
public class StudentAssignmentStats
{
    public int TotalAssigned { get; set; }
    public int AssignedCount { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int Skipped { get; set; }
    public decimal AverageScore { get; set; }
    public int TotalPointsEarned { get; set; }
    public int PerfectScores { get; set; }
    public TimeSpan AverageCompletionTime { get; set; }
}

/// <summary>
/// Overall assignment statistics.
/// </summary>
public class AssignmentStats
{
    public int TotalAssignments { get; set; }
    public int DraftCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int ArchivedCount { get; set; }
    public int TotalSubmissions { get; set; }
    public decimal AverageScore { get; set; }
    public int AiGeneratedCount { get; set; }
    public int ManuallyCreatedCount { get; set; }
    public int FromLibraryCount { get; set; }
    public decimal LibraryReuseRate => TotalAssignments > 0
        ? (decimal)FromLibraryCount / TotalAssignments * 100 : 0;
}
