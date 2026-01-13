using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Text.Json;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = "Student")]
public class AssignmentController : Controller
{
    private readonly IAssignmentService _assignmentService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AssignmentController(
        IAssignmentService assignmentService,
        UserManager<ApplicationUser> userManager)
    {
        _assignmentService = assignmentService;
        _userManager = userManager;
    }

    /// <summary>
    /// List of student's assignments.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(StudentAssignmentStatus? status)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var assignments = await _assignmentService.GetStudentAssignmentsAsync(user.Id, status);
        var stats = await _assignmentService.GetStudentStatsAsync(user.Id);
        var submissions = await _assignmentService.GetStudentSubmissionsAsync(user.Id);

        // Group submissions by assignment and get max points earned
        var bestPointsByAssignment = submissions
            .GroupBy(s => s.AssignmentId)
            .ToDictionary(g => g.Key, g => g.Max(s => s.TotalPointsEarned));

        var viewModels = assignments.Select(sa => new StudentAssignmentViewModel
        {
            StudentAssignmentId = sa.Id,
            AssignmentId = sa.AssignmentId,
            Title = sa.Assignment.Title,
            Description = sa.Assignment.Description,
            CefrLevel = sa.Assignment.CefrLevel,
            Type = sa.Assignment.Type,
            TopicName = sa.Assignment.CurriculumTopic?.Name,
            TotalPoints = sa.Assignment.TotalPoints,
            BonusPoints = sa.Assignment.BonusPoints,
            EstimatedMinutes = sa.Assignment.EstimatedMinutes,
            Status = sa.Status,
            AssignedAt = sa.AssignedAt,
            DueDate = sa.DueDate,
            StartedAt = sa.StartedAt,
            CompletedAt = sa.CompletedAt,
            BestScore = sa.BestScore,
            AttemptCount = sa.AttemptCount,
            PointsEarned = bestPointsByAssignment.GetValueOrDefault(sa.AssignmentId)
        }).ToList();

        return View(new StudentAssignmentListViewModel
        {
            Assignments = viewModels,
            Stats = stats
        });
    }

    /// <summary>
    /// Start/take an assignment.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Take(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var studentAssignment = await _assignmentService.GetStudentAssignmentAsync(id);
        if (studentAssignment == null || studentAssignment.StudentId != user.Id)
        {
            return NotFound();
        }

        if (studentAssignment.Assignment.Status != AssignmentStatus.Approved)
        {
            TempData["ErrorMessage"] = "This assignment is not available.";
            return RedirectToAction(nameof(Index));
        }

        // Mark as started if not already
        if (studentAssignment.Status == StudentAssignmentStatus.Assigned)
        {
            await _assignmentService.StartAssignmentAsync(id);
        }

        // Get attempt count
        var submissions = await _assignmentService.GetStudentSubmissionsAsync(user.Id);
        var attemptCount = submissions.Count(s => s.AssignmentId == studentAssignment.AssignmentId) + 1;

        var viewModel = new TakeAssignmentViewModel
        {
            StudentAssignmentId = id,
            AssignmentId = studentAssignment.AssignmentId,
            Title = studentAssignment.Assignment.Title,
            Description = studentAssignment.Assignment.Description,
            CefrLevel = studentAssignment.Assignment.CefrLevel,
            Type = studentAssignment.Assignment.Type,
            TotalPoints = studentAssignment.Assignment.TotalPoints,
            BonusPoints = studentAssignment.Assignment.BonusPoints,
            EstimatedMinutes = studentAssignment.Assignment.EstimatedMinutes,
            QuestionsJson = studentAssignment.Assignment.QuestionsJson,
            AttemptNumber = attemptCount
        };

        return View(viewModel);
    }

    /// <summary>
    /// Submit assignment answers.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitAssignmentViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        try
        {
            var result = await _assignmentService.SubmitAssignmentAsync(
                user.Id,
                model.AssignmentId,
                model.AnswersJson,
                model.TimeTakenSeconds,
                model.DifficultyFeedback,
                model.StudentNotes);

            return RedirectToAction(nameof(Results), new { id = result.Submission.Id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error submitting assignment: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// View assignment results.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var submission = await _assignmentService.GetSubmissionByIdAsync(id);
        if (submission == null || submission.StudentId != user.Id)
        {
            return NotFound();
        }

        // Parse grading results
        var questionResults = new List<QuestionResultViewModel>();
        try
        {
            using var gradingDoc = JsonDocument.Parse(submission.GradingResultJson);
            using var questionsDoc = JsonDocument.Parse(submission.Assignment.QuestionsJson);

            var questions = questionsDoc.RootElement.EnumerateArray()
                .ToDictionary(q => q.GetProperty("id").GetInt32(), q => q);

            foreach (var result in gradingDoc.RootElement.EnumerateArray())
            {
                var questionId = result.GetProperty("questionId").GetInt32();
                var questionText = "";

                if (questions.TryGetValue(questionId, out var q))
                {
                    questionText = q.TryGetProperty("question", out var qProp) ? qProp.GetString() ?? "" : "";
                }

                questionResults.Add(new QuestionResultViewModel
                {
                    QuestionId = questionId,
                    Question = questionText,
                    IsCorrect = result.GetProperty("isCorrect").GetBoolean(),
                    StudentAnswer = result.TryGetProperty("studentAnswer", out var sa) ? sa.GetString() ?? "" : "",
                    CorrectAnswer = result.TryGetProperty("correctAnswer", out var ca) ? ca.GetString() ?? "" : "",
                    Feedback = result.TryGetProperty("feedback", out var fb) ? fb.GetString() : null,
                    Explanation = result.TryGetProperty("explanation", out var exp) ? exp.GetString() : null
                });
            }
        }
        catch
        {
            // If parsing fails, show basic results
        }

        var viewModel = new AssignmentResultViewModel
        {
            SubmissionId = submission.Id,
            AssignmentTitle = submission.Assignment.Title,
            CorrectAnswers = submission.CorrectAnswers,
            TotalQuestions = submission.TotalQuestions,
            PercentageScore = submission.PercentageScore,
            PointsEarned = submission.PointsEarned,
            BonusPointsEarned = submission.BonusPointsEarned,
            IsPerfectScore = submission.PercentageScore == 100,
            IsFirstAttempt = submission.IsFirstAttempt,
            TimeTaken = TimeSpan.FromSeconds(submission.TimeTakenSeconds),
            QuestionResults = questionResults
        };

        return View(viewModel);
    }

    /// <summary>
    /// View submission history.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> History()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var submissions = await _assignmentService.GetStudentSubmissionsAsync(user.Id, 50);

        return View(submissions);
    }

    /// <summary>
    /// Skip an assignment.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Skip(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var studentAssignment = await _assignmentService.GetStudentAssignmentAsync(id);
        if (studentAssignment == null || studentAssignment.StudentId != user.Id)
        {
            return NotFound();
        }

        await _assignmentService.SkipAssignmentAsync(id);

        TempData["InfoMessage"] = "Assignment skipped.";
        return RedirectToAction(nameof(Index));
    }
}
