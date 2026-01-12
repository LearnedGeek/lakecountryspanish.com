using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services;

public class AssignmentService : IAssignmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IClaudeApiService _claudeApiService;
    private readonly IGamificationService _gamificationService;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        ApplicationDbContext context,
        IClaudeApiService claudeApiService,
        IGamificationService gamificationService,
        ILogger<AssignmentService> logger)
    {
        _context = context;
        _claudeApiService = claudeApiService;
        _gamificationService = gamificationService;
        _logger = logger;
    }

    // Curriculum Topics

    public async Task<IEnumerable<CurriculumTopic>> GetAllTopicsAsync()
    {
        return await _context.CurriculumTopics
            .Where(t => t.IsActive)
            .OrderBy(t => t.CefrLevel)
            .ThenBy(t => t.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<CurriculumTopic>> GetTopicsByLevelAsync(string cefrLevel)
    {
        return await _context.CurriculumTopics
            .Where(t => t.CefrLevel == cefrLevel && t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();
    }

    public async Task<CurriculumTopic?> GetTopicByIdAsync(int topicId)
    {
        return await _context.CurriculumTopics.FindAsync(topicId);
    }

    public async Task<CurriculumTopic> CreateTopicAsync(CurriculumTopic topic)
    {
        _context.CurriculumTopics.Add(topic);
        await _context.SaveChangesAsync();
        return topic;
    }

    public async Task<CurriculumTopic?> UpdateTopicAsync(CurriculumTopic topic)
    {
        var existing = await _context.CurriculumTopics.FindAsync(topic.Id);
        if (existing == null) return null;

        existing.Name = topic.Name;
        existing.Description = topic.Description;
        existing.CefrLevel = topic.CefrLevel;
        existing.Type = topic.Type;
        existing.DisplayOrder = topic.DisplayOrder;
        existing.IsActive = topic.IsActive;
        existing.Keywords = topic.Keywords;
        existing.ExampleContent = topic.ExampleContent;

        await _context.SaveChangesAsync();
        return existing;
    }

    // Assignments

    public async Task<IEnumerable<Assignment>> GetAssignmentsAsync(
        string? cefrLevel = null,
        AssignmentStatus? status = null,
        AssignmentType? type = null)
    {
        var query = _context.Assignments
            .Include(a => a.CurriculumTopic)
            .AsQueryable();

        if (!string.IsNullOrEmpty(cefrLevel))
            query = query.Where(a => a.CefrLevel == cefrLevel);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (type.HasValue)
            query = query.Where(a => a.Type == type.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<Assignment?> GetAssignmentByIdAsync(int assignmentId)
    {
        return await _context.Assignments
            .Include(a => a.CurriculumTopic)
            .FirstOrDefaultAsync(a => a.Id == assignmentId);
    }

    public async Task<Assignment> GenerateAssignmentAsync(
        string cefrLevel,
        AssignmentType type,
        int? topicId = null,
        int questionCount = 5,
        string? additionalInstructions = null,
        string? createdById = null)
    {
        CurriculumTopic? topic = null;
        if (topicId.HasValue)
        {
            topic = await GetTopicByIdAsync(topicId.Value);
        }

        var request = new AssignmentGenerationRequest
        {
            CefrLevel = cefrLevel,
            Type = type,
            TopicName = topic?.Name,
            TopicDescription = topic?.Description,
            Keywords = topic?.Keywords,
            QuestionCount = questionCount,
            AdditionalInstructions = additionalInstructions
        };

        var generated = await _claudeApiService.GenerateAssignmentAsync(request);

        var assignment = new Assignment
        {
            Title = generated.Success ? generated.Title : $"Draft {type} Assignment",
            Description = generated.Success ? generated.Description : "Pending content generation",
            CefrLevel = cefrLevel,
            CurriculumTopicId = topicId,
            Type = type,
            Status = generated.Success ? AssignmentStatus.PendingReview : AssignmentStatus.Draft,
            QuestionsJson = generated.QuestionsJson,
            AnswersJson = generated.AnswersJson,
            TotalPoints = generated.TotalPoints > 0 ? generated.TotalPoints : questionCount * 2,
            EstimatedMinutes = generated.EstimatedMinutes > 0 ? generated.EstimatedMinutes : 10,
            IsAiGenerated = true,
            GenerationPrompt = generated.GenerationPrompt,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };

        if (!generated.Success)
        {
            _logger.LogWarning("AI generation failed: {Error}", generated.ErrorMessage);
        }

        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        return assignment;
    }

    public async Task<Assignment> CreateAssignmentAsync(Assignment assignment)
    {
        assignment.IsAiGenerated = false;
        assignment.CreatedAt = DateTime.UtcNow;
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();
        return assignment;
    }

    public async Task<Assignment?> UpdateAssignmentAsync(Assignment assignment)
    {
        var existing = await _context.Assignments.FindAsync(assignment.Id);
        if (existing == null) return null;

        existing.Title = assignment.Title;
        existing.Description = assignment.Description;
        existing.CefrLevel = assignment.CefrLevel;
        existing.CurriculumTopicId = assignment.CurriculumTopicId;
        existing.Type = assignment.Type;
        existing.QuestionsJson = assignment.QuestionsJson;
        existing.AnswersJson = assignment.AnswersJson;
        existing.TotalPoints = assignment.TotalPoints;
        existing.BonusPoints = assignment.BonusPoints;
        existing.EstimatedMinutes = assignment.EstimatedMinutes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> ApproveAssignmentAsync(int assignmentId, string reviewerId, string? notes = null)
    {
        var assignment = await _context.Assignments.FindAsync(assignmentId);
        if (assignment == null) return false;

        assignment.Status = AssignmentStatus.Approved;
        assignment.ReviewedById = reviewerId;
        assignment.ReviewedAt = DateTime.UtcNow;
        assignment.ReviewNotes = notes;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAssignmentAsync(int assignmentId, string reviewerId, string? notes = null)
    {
        var assignment = await _context.Assignments.FindAsync(assignmentId);
        if (assignment == null) return false;

        assignment.Status = AssignmentStatus.Rejected;
        assignment.ReviewedById = reviewerId;
        assignment.ReviewedAt = DateTime.UtcNow;
        assignment.ReviewNotes = notes;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ArchiveAssignmentAsync(int assignmentId)
    {
        var assignment = await _context.Assignments.FindAsync(assignmentId);
        if (assignment == null) return false;

        assignment.Status = AssignmentStatus.Archived;
        await _context.SaveChangesAsync();
        return true;
    }

    // Student Assignments

    public async Task<IEnumerable<StudentAssignment>> GetStudentAssignmentsAsync(
        string studentId,
        StudentAssignmentStatus? status = null)
    {
        var query = _context.StudentAssignments
            .Include(sa => sa.Assignment)
                .ThenInclude(a => a.CurriculumTopic)
            .Where(sa => sa.StudentId == studentId);

        if (status.HasValue)
            query = query.Where(sa => sa.Status == status.Value);

        return await query
            .OrderBy(sa => sa.Priority)
            .ThenBy(sa => sa.DueDate)
            .ThenByDescending(sa => sa.AssignedAt)
            .ToListAsync();
    }

    public async Task<StudentAssignment> AssignToStudentAsync(
        string studentId,
        int assignmentId,
        string? assignedById = null,
        DateTime? dueDate = null,
        string? notes = null,
        int priority = 0)
    {
        // Check if already assigned
        var existing = await _context.StudentAssignments
            .FirstOrDefaultAsync(sa => sa.StudentId == studentId && sa.AssignmentId == assignmentId);

        if (existing != null)
        {
            // Reset if previously completed/skipped
            if (existing.Status == StudentAssignmentStatus.Completed ||
                existing.Status == StudentAssignmentStatus.Skipped)
            {
                existing.Status = StudentAssignmentStatus.Assigned;
                existing.AssignedAt = DateTime.UtcNow;
                existing.DueDate = dueDate;
                existing.StartedAt = null;
                existing.CompletedAt = null;
                await _context.SaveChangesAsync();
            }
            return existing;
        }

        var studentAssignment = new StudentAssignment
        {
            StudentId = studentId,
            AssignmentId = assignmentId,
            AssignedById = assignedById,
            AssignedAt = DateTime.UtcNow,
            DueDate = dueDate,
            AssignerNotes = notes,
            Priority = priority,
            IsAutoAssigned = assignedById == null
        };

        _context.StudentAssignments.Add(studentAssignment);
        await _context.SaveChangesAsync();

        return studentAssignment;
    }

    public async Task<IEnumerable<StudentAssignment>> AssignToStudentsAsync(
        IEnumerable<string> studentIds,
        int assignmentId,
        string? assignedById = null,
        DateTime? dueDate = null)
    {
        var results = new List<StudentAssignment>();

        foreach (var studentId in studentIds)
        {
            var sa = await AssignToStudentAsync(studentId, assignmentId, assignedById, dueDate);
            results.Add(sa);
        }

        return results;
    }

    public async Task<StudentAssignment?> GetStudentAssignmentAsync(int studentAssignmentId)
    {
        return await _context.StudentAssignments
            .Include(sa => sa.Assignment)
                .ThenInclude(a => a.CurriculumTopic)
            .FirstOrDefaultAsync(sa => sa.Id == studentAssignmentId);
    }

    public async Task<StudentAssignment?> StartAssignmentAsync(int studentAssignmentId)
    {
        var sa = await _context.StudentAssignments.FindAsync(studentAssignmentId);
        if (sa == null) return null;

        if (sa.Status == StudentAssignmentStatus.Assigned)
        {
            sa.Status = StudentAssignmentStatus.InProgress;
            sa.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return sa;
    }

    public async Task<bool> SkipAssignmentAsync(int studentAssignmentId)
    {
        var sa = await _context.StudentAssignments.FindAsync(studentAssignmentId);
        if (sa == null) return false;

        sa.Status = StudentAssignmentStatus.Skipped;
        await _context.SaveChangesAsync();
        return true;
    }

    // Submissions

    public async Task<SubmissionResult> SubmitAssignmentAsync(
        string studentId,
        int assignmentId,
        string answersJson,
        int timeTakenSeconds,
        DifficultyFeedback? difficultyFeedback = null,
        string? studentNotes = null)
    {
        var assignment = await GetAssignmentByIdAsync(assignmentId);
        if (assignment == null)
        {
            throw new ArgumentException("Assignment not found", nameof(assignmentId));
        }

        // Get previous attempts
        var previousAttempts = await _context.AssignmentSubmissions
            .Where(s => s.StudentId == studentId && s.AssignmentId == assignmentId)
            .CountAsync();

        var isFirstAttempt = previousAttempts == 0;

        // Grade the submission
        var questionResults = await GradeSubmissionAsync(assignment, answersJson);
        var correctCount = questionResults.Count(q => q.IsCorrect);
        var totalQuestions = questionResults.Count();
        var percentageScore = totalQuestions > 0 ? (decimal)correctCount / totalQuestions * 100 : 0;
        var isPerfectScore = correctCount == totalQuestions && totalQuestions > 0;

        // Calculate points
        var pointsPerQuestion = assignment.TotalPoints / Math.Max(1, totalQuestions);
        var pointsEarned = correctCount * pointsPerQuestion;
        var bonusPointsEarned = isPerfectScore ? assignment.BonusPoints : 0;

        // Create submission
        var submission = new AssignmentSubmission
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            AnswersJson = answersJson,
            GradingResultJson = JsonSerializer.Serialize(questionResults),
            CorrectAnswers = correctCount,
            TotalQuestions = totalQuestions,
            PercentageScore = percentageScore,
            PointsEarned = pointsEarned,
            BonusPointsEarned = bonusPointsEarned,
            TimeTakenSeconds = timeTakenSeconds,
            DifficultyFeedback = difficultyFeedback,
            StudentNotes = studentNotes,
            IsFirstAttempt = isFirstAttempt,
            AttemptNumber = previousAttempts + 1,
            StartedAt = DateTime.UtcNow.AddSeconds(-timeTakenSeconds),
            SubmittedAt = DateTime.UtcNow
        };

        _context.AssignmentSubmissions.Add(submission);

        // Update student assignment status
        var studentAssignment = await _context.StudentAssignments
            .FirstOrDefaultAsync(sa => sa.StudentId == studentId && sa.AssignmentId == assignmentId);

        if (studentAssignment != null)
        {
            studentAssignment.Status = StudentAssignmentStatus.Completed;
            studentAssignment.CompletedAt = DateTime.UtcNow;
            studentAssignment.AttemptCount++;
            if (!studentAssignment.BestScore.HasValue || percentageScore > studentAssignment.BestScore)
            {
                studentAssignment.BestScore = percentageScore;
            }
        }

        await _context.SaveChangesAsync();

        // Award points via gamification service
        var totalPoints = pointsEarned + bonusPointsEarned;
        if (totalPoints > 0)
        {
            await _gamificationService.AwardPointsAsync(
                studentId,
                totalPoints,
                isPerfectScore ? PointSource.PerfectScore : PointSource.Exercise,
                details: $"Assignment: {assignment.Title} ({percentageScore:F0}%)");

            // Record activity for streak
            await _gamificationService.RecordActivityAsync(studentId);
        }

        return new SubmissionResult
        {
            Submission = submission,
            PointsEarned = pointsEarned,
            BonusPointsEarned = bonusPointsEarned,
            IsPerfectScore = isPerfectScore,
            IsFirstAttempt = isFirstAttempt,
            QuestionResults = questionResults
        };
    }

    private async Task<List<QuestionResult>> GradeSubmissionAsync(Assignment assignment, string answersJson)
    {
        var results = new List<QuestionResult>();

        try
        {
            using var questionsDoc = JsonDocument.Parse(assignment.QuestionsJson);
            using var correctAnswersDoc = JsonDocument.Parse(assignment.AnswersJson);
            using var studentAnswersDoc = JsonDocument.Parse(answersJson);

            var correctAnswers = correctAnswersDoc.RootElement.EnumerateArray()
                .ToDictionary(
                    a => a.GetProperty("id").GetInt32(),
                    a => a);

            var studentAnswers = studentAnswersDoc.RootElement.EnumerateArray()
                .ToDictionary(
                    a => a.GetProperty("id").GetInt32(),
                    a => a.GetProperty("answer").GetString() ?? "");

            foreach (var question in questionsDoc.RootElement.EnumerateArray())
            {
                var questionId = question.GetProperty("id").GetInt32();
                var studentAnswer = studentAnswers.GetValueOrDefault(questionId, "");

                if (!correctAnswers.TryGetValue(questionId, out var correctAnswerElement))
                {
                    continue;
                }

                var correctAnswer = correctAnswerElement.GetProperty("answer").GetString() ?? "";
                var explanation = correctAnswerElement.TryGetProperty("explanation", out var exp)
                    ? exp.GetString() : null;

                // Check acceptable answers
                var isCorrect = string.Equals(studentAnswer.Trim(), correctAnswer.Trim(),
                    StringComparison.OrdinalIgnoreCase);

                if (!isCorrect && correctAnswerElement.TryGetProperty("acceptableAnswers", out var acceptable))
                {
                    foreach (var alt in acceptable.EnumerateArray())
                    {
                        if (string.Equals(studentAnswer.Trim(), alt.GetString()?.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                        {
                            isCorrect = true;
                            break;
                        }
                    }
                }

                results.Add(new QuestionResult
                {
                    QuestionId = questionId,
                    IsCorrect = isCorrect,
                    StudentAnswer = studentAnswer,
                    CorrectAnswer = correctAnswer,
                    Feedback = isCorrect ? "Correct!" : "Incorrect",
                    Explanation = explanation
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grading submission");
        }

        return results;
    }

    public async Task<IEnumerable<AssignmentSubmission>> GetStudentSubmissionsAsync(
        string studentId,
        int? limit = null)
    {
        var query = _context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.SubmittedAt);

        if (limit.HasValue)
            return await query.Take(limit.Value).ToListAsync();

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<AssignmentSubmission>> GetAssignmentSubmissionsAsync(int assignmentId)
    {
        return await _context.AssignmentSubmissions
            .Include(s => s.Student)
            .Where(s => s.AssignmentId == assignmentId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }

    public async Task<AssignmentSubmission?> GetSubmissionByIdAsync(int submissionId)
    {
        return await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .FirstOrDefaultAsync(s => s.Id == submissionId);
    }

    // Statistics

    public async Task<StudentAssignmentStats> GetStudentStatsAsync(string studentId)
    {
        var assignments = await _context.StudentAssignments
            .Where(sa => sa.StudentId == studentId)
            .ToListAsync();

        var submissions = await _context.AssignmentSubmissions
            .Where(s => s.StudentId == studentId)
            .ToListAsync();

        var assignedCount = assignments.Count(a => a.Status == StudentAssignmentStatus.Assigned);
        var inProgressCount = assignments.Count(a => a.Status == StudentAssignmentStatus.InProgress);
        var completedCount = assignments.Count(a => a.Status == StudentAssignmentStatus.Completed);

        return new StudentAssignmentStats
        {
            TotalAssigned = assignments.Count,
            AssignedCount = assignedCount,
            InProgressCount = inProgressCount,
            CompletedCount = completedCount,
            Skipped = assignments.Count(a => a.Status == StudentAssignmentStatus.Skipped),
            AverageScore = submissions.Any() ? submissions.Average(s => s.PercentageScore) : 0,
            TotalPointsEarned = submissions.Sum(s => s.TotalPointsEarned),
            PerfectScores = submissions.Count(s => s.PercentageScore == 100),
            AverageCompletionTime = submissions.Any()
                ? TimeSpan.FromSeconds(submissions.Average(s => s.TimeTakenSeconds))
                : TimeSpan.Zero
        };
    }

    public async Task<AssignmentStats> GetOverallStatsAsync()
    {
        var assignments = await _context.Assignments.ToListAsync();
        var submissions = await _context.AssignmentSubmissions.ToListAsync();

        return new AssignmentStats
        {
            TotalAssignments = assignments.Count,
            DraftCount = assignments.Count(a => a.Status == AssignmentStatus.Draft),
            PendingReviewCount = assignments.Count(a => a.Status == AssignmentStatus.PendingReview),
            ApprovedCount = assignments.Count(a => a.Status == AssignmentStatus.Approved),
            RejectedCount = assignments.Count(a => a.Status == AssignmentStatus.Rejected),
            ArchivedCount = assignments.Count(a => a.Status == AssignmentStatus.Archived),
            TotalSubmissions = submissions.Count,
            AverageScore = submissions.Any() ? submissions.Average(s => s.PercentageScore) : 0,
            AiGeneratedCount = assignments.Count(a => a.IsAiGenerated),
            ManuallyCreatedCount = assignments.Count(a => !a.IsAiGenerated)
        };
    }
}
