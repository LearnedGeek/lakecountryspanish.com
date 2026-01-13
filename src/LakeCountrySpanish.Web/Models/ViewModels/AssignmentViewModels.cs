using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Models.ViewModels;

// Student-facing view models

/// <summary>
/// View model for student's assignment list.
/// </summary>
public class StudentAssignmentListViewModel
{
    public IEnumerable<StudentAssignmentViewModel> Assignments { get; set; } = new List<StudentAssignmentViewModel>();
    public StudentAssignmentStats Stats { get; set; } = new();
}

/// <summary>
/// View model for a single assignment in student's list.
/// </summary>
public class StudentAssignmentViewModel
{
    public int StudentAssignmentId { get; set; }
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = string.Empty;
    public AssignmentType Type { get; set; }
    public string? TopicName { get; set; }
    public int TotalPoints { get; set; }
    public int BonusPoints { get; set; }
    public int EstimatedMinutes { get; set; }
    public StudentAssignmentStatus Status { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? BestScore { get; set; }
    public int AttemptCount { get; set; }
    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.UtcNow && Status != StudentAssignmentStatus.Completed;

    public string TypeDisplayName => Type switch
    {
        AssignmentType.FillInTheBlank => "Fill in the Blank",
        AssignmentType.MultipleChoice => "Multiple Choice",
        AssignmentType.Translation => "Translation",
        AssignmentType.Matching => "Matching",
        AssignmentType.ShortAnswer => "Short Answer",
        AssignmentType.Reading => "Reading",
        AssignmentType.Listening => "Listening",
        AssignmentType.Mixed => "Mixed",
        _ => Type.ToString()
    };

    public string StatusBadgeClass => Status switch
    {
        StudentAssignmentStatus.Assigned => "bg-primary",
        StudentAssignmentStatus.InProgress => "bg-warning",
        StudentAssignmentStatus.Completed => "bg-success",
        StudentAssignmentStatus.Skipped => "bg-secondary",
        StudentAssignmentStatus.Expired => "bg-danger",
        _ => "bg-secondary"
    };
}

/// <summary>
/// View model for taking an assignment.
/// </summary>
public class TakeAssignmentViewModel
{
    public int StudentAssignmentId { get; set; }
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = string.Empty;
    public AssignmentType Type { get; set; }
    public int TotalPoints { get; set; }
    public int BonusPoints { get; set; }
    public int EstimatedMinutes { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public int AttemptNumber { get; set; }
}

/// <summary>
/// View model for submitting an assignment.
/// </summary>
public class SubmitAssignmentViewModel
{
    public int AssignmentId { get; set; }
    public string AnswersJson { get; set; } = "[]";
    public int TimeTakenSeconds { get; set; }
    public DifficultyFeedback? DifficultyFeedback { get; set; }
    public string? StudentNotes { get; set; }
}

/// <summary>
/// View model for assignment results.
/// </summary>
public class AssignmentResultViewModel
{
    public int SubmissionId { get; set; }
    public string AssignmentTitle { get; set; } = string.Empty;
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public decimal PercentageScore { get; set; }
    public int PointsEarned { get; set; }
    public int BonusPointsEarned { get; set; }
    public bool IsPerfectScore { get; set; }
    public bool IsFirstAttempt { get; set; }
    public TimeSpan TimeTaken { get; set; }
    public IEnumerable<QuestionResultViewModel> QuestionResults { get; set; } = new List<QuestionResultViewModel>();
}

/// <summary>
/// View model for a single question result.
/// </summary>
public class QuestionResultViewModel
{
    public int QuestionId { get; set; }
    public string Question { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string StudentAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Feedback { get; set; }
    public string? Explanation { get; set; }
}

// Admin-facing view models

/// <summary>
/// View model for admin assignment list.
/// </summary>
public class AdminAssignmentListViewModel
{
    public IEnumerable<AdminAssignmentViewModel> Assignments { get; set; } = new List<AdminAssignmentViewModel>();
    public AssignmentStats Stats { get; set; } = new();
    public string? FilterCefrLevel { get; set; }
    public AssignmentStatus? FilterStatus { get; set; }
    public AssignmentType? FilterType { get; set; }
}

/// <summary>
/// View model for assignment in admin list.
/// </summary>
public class AdminAssignmentViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = string.Empty;
    public AssignmentType Type { get; set; }
    public AssignmentStatus Status { get; set; }
    public string? TopicName { get; set; }
    public int TotalPoints { get; set; }
    public int EstimatedMinutes { get; set; }
    public bool IsAiGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int SubmissionCount { get; set; }
    public decimal? AverageScore { get; set; }

    public string StatusBadgeClass => Status switch
    {
        AssignmentStatus.Draft => "bg-secondary",
        AssignmentStatus.PendingReview => "bg-warning",
        AssignmentStatus.Approved => "bg-success",
        AssignmentStatus.Rejected => "bg-danger",
        AssignmentStatus.Archived => "bg-dark",
        _ => "bg-secondary"
    };
}

/// <summary>
/// View model for generating an assignment.
/// </summary>
public class GenerateAssignmentViewModel
{
    public string CefrLevel { get; set; } = "A1";
    public AssignmentType Type { get; set; } = AssignmentType.MultipleChoice;
    public int? TopicId { get; set; }
    public int QuestionCount { get; set; } = 5;
    public string? AdditionalInstructions { get; set; }
    public bool ForceNewGeneration { get; set; }

    public IEnumerable<CurriculumTopic> AvailableTopics { get; set; } = new List<CurriculumTopic>();
    public IEnumerable<Assignment> LibraryMatches { get; set; } = new List<Assignment>();
    public bool ShowLibraryOptions => LibraryMatches.Any();
}

/// <summary>
/// View model for creating/editing an assignment.
/// </summary>
public class EditAssignmentViewModel
{
    public int? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = "A1";
    public int? CurriculumTopicId { get; set; }
    public AssignmentType Type { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public string AnswersJson { get; set; } = "[]";
    public int TotalPoints { get; set; } = 10;
    public int BonusPoints { get; set; } = 0;
    public int EstimatedMinutes { get; set; } = 10;

    public bool IsNew => !Id.HasValue;
    public IEnumerable<CurriculumTopic> AvailableTopics { get; set; } = new List<CurriculumTopic>();
}

/// <summary>
/// View model for reviewing an assignment.
/// </summary>
public class ReviewAssignmentViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = string.Empty;
    public AssignmentType Type { get; set; }
    public string? TopicName { get; set; }
    public int TotalPoints { get; set; }
    public int EstimatedMinutes { get; set; }
    public bool IsAiGenerated { get; set; }
    public string? GenerationPrompt { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public string AnswersJson { get; set; } = "[]";
    public string? ReviewNotes { get; set; }
}

/// <summary>
/// View model for assigning to students.
/// </summary>
public class AssignToStudentsViewModel
{
    public int AssignmentId { get; set; }
    public string AssignmentTitle { get; set; } = string.Empty;
    public string? AssignmentDescription { get; set; }
    public string CefrLevel { get; set; } = string.Empty;
    public AssignmentType AssignmentType { get; set; }
    public int TotalPoints { get; set; }
    public List<string> SelectedStudentIds { get; set; } = new();
    public DateTime? DueDate { get; set; }
    public IEnumerable<StudentSelectViewModel> AvailableStudents { get; set; } = new List<StudentSelectViewModel>();
}

/// <summary>
/// View model for student selection.
/// </summary>
public class StudentSelectViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CefrLevel { get; set; }
    public bool AlreadyAssigned { get; set; }
}

/// <summary>
/// View model for viewing assignment details (read-only).
/// </summary>
public class ViewAssignmentViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = string.Empty;
    public AssignmentType Type { get; set; }
    public AssignmentStatus Status { get; set; }
    public string? TopicName { get; set; }
    public int TotalPoints { get; set; }
    public int BonusPoints { get; set; }
    public int EstimatedMinutes { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public bool IsAiGenerated { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public int SubmissionCount { get; set; }
    public decimal? AverageScore { get; set; }
    public List<AssignedStudentInfo> AssignedStudents { get; set; } = new();
}

/// <summary>
/// Info about a student assigned to an assignment.
/// </summary>
public class AssignedStudentInfo
{
    public string StudentName { get; set; } = string.Empty;
    public StudentAssignmentStatus Status { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? BestScore { get; set; }
}

// Curriculum Topic view models

/// <summary>
/// View model for curriculum topic list.
/// </summary>
public class CurriculumTopicListViewModel
{
    public IEnumerable<CurriculumTopic> Topics { get; set; } = new List<CurriculumTopic>();
    public string? FilterCefrLevel { get; set; }
    public TopicType? FilterType { get; set; }
}

/// <summary>
/// View model for editing a curriculum topic.
/// </summary>
public class EditTopicViewModel
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = "A1";
    public TopicType Type { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Keywords { get; set; }
    public string? ExampleContent { get; set; }

    public bool IsNew => !Id.HasValue;
}
