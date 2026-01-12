using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

public class PlacementTestService : IPlacementTestService
{
    private readonly ApplicationDbContext _context;
    private readonly IClaudeApiService _claudeApiService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<PlacementTestService> _logger;

    private const int QuestionsPerLevel = 5;
    private const int MaxTotalQuestions = 30;
    private const decimal AdvanceThreshold = 0.80m; // 80% to advance
    private const decimal DropThreshold = 0.60m;    // Below 60% to drop
    private const int MaxFailedAdvances = 2;        // Stop after failing to advance twice

    private static readonly string[] CefrLevels = { "A1", "A2", "B1", "B2", "C1", "C2" };

    public PlacementTestService(
        ApplicationDbContext context,
        IClaudeApiService claudeApiService,
        UserManager<ApplicationUser> userManager,
        ILogger<PlacementTestService> logger)
    {
        _context = context;
        _claudeApiService = claudeApiService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<PlacementTestSession> StartTestAsync(string studentId)
    {
        // Check for existing in-progress test
        var existingSession = await GetActiveSessionAsync(studentId);
        if (existingSession != null)
        {
            return existingSession;
        }

        var session = new PlacementTestSession
        {
            StudentId = studentId,
            Status = PlacementTestStatus.InProgress,
            StartingLevel = "A2",
            CurrentLevel = "A2",
            StartedAt = DateTime.UtcNow
        };

        _context.PlacementTestSessions.Add(session);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Started placement test session {SessionId} for student {StudentId}",
            session.Id, studentId);

        return session;
    }

    public async Task<PlacementTestQuestionViewModel?> GetNextQuestionAsync(int sessionId)
    {
        var session = await GetSessionByIdAsync(sessionId);
        if (session == null || session.Status != PlacementTestStatus.InProgress)
        {
            return null;
        }

        // Check if we've hit the max questions
        if (session.TotalQuestionsAnswered >= MaxTotalQuestions)
        {
            await FinalizeTestAsync(session);
            return null;
        }

        // Get previously used topics to avoid repetition
        var previousTopics = GetPreviousTopics(session);

        // Generate a question for the current level
        var question = await _claudeApiService.GeneratePlacementQuestionAsync(
            session.CurrentLevel, previousTopics);

        if (!question.Success)
        {
            _logger.LogWarning("Failed to generate placement question: {Error}", question.ErrorMessage);
            // Return a fallback question if API fails
            question = GetFallbackQuestion(session.CurrentLevel);
        }

        // Store the current question in session (will be used when grading)
        await StoreCurrentQuestionAsync(session, question);

        return new PlacementTestQuestionViewModel
        {
            SessionId = sessionId,
            QuestionNumber = session.TotalQuestionsAnswered + 1,
            CurrentLevel = session.CurrentLevel,
            QuestionType = question.Type,
            QuestionText = question.Question,
            Options = question.Options,
            Hint = question.Hint,
            TotalAnswered = session.TotalQuestionsAnswered,
            MaxQuestions = MaxTotalQuestions
        };
    }

    public async Task<PlacementTestAnswerFeedbackViewModel> SubmitAnswerAsync(int sessionId, string answer)
    {
        var session = await GetSessionByIdAsync(sessionId);
        if (session == null || session.Status != PlacementTestStatus.InProgress)
        {
            return new PlacementTestAnswerFeedbackViewModel
            {
                SessionId = sessionId,
                IsCorrect = false,
                CorrectAnswer = "",
                TestComplete = true
            };
        }

        // Get the current question from session
        var currentQuestion = GetCurrentQuestion(session);
        if (currentQuestion == null)
        {
            return new PlacementTestAnswerFeedbackViewModel
            {
                SessionId = sessionId,
                IsCorrect = false,
                CorrectAnswer = "",
                TestComplete = false
            };
        }

        // Grade the answer
        var isCorrect = GradeAnswer(answer, currentQuestion);

        // Update session statistics
        session.TotalQuestionsAnswered++;
        if (isCorrect) session.TotalCorrect++;

        // Update level progress
        UpdateLevelProgress(session, isCorrect);

        // Record the answer
        await RecordAnswerAsync(session, currentQuestion, answer, isCorrect);

        // Check if we should change levels or end the test
        var (shouldContinue, newLevel) = EvaluateProgress(session);

        if (!shouldContinue || session.TotalQuestionsAnswered >= MaxTotalQuestions)
        {
            await FinalizeTestAsync(session);
            return new PlacementTestAnswerFeedbackViewModel
            {
                SessionId = sessionId,
                IsCorrect = isCorrect,
                CorrectAnswer = currentQuestion.CorrectAnswer,
                Explanation = currentQuestion.Explanation,
                TestComplete = true,
                DeterminedLevel = session.DeterminedLevel
            };
        }

        if (newLevel != session.CurrentLevel)
        {
            session.CurrentLevel = newLevel;
        }

        await _context.SaveChangesAsync();

        return new PlacementTestAnswerFeedbackViewModel
        {
            SessionId = sessionId,
            IsCorrect = isCorrect,
            CorrectAnswer = currentQuestion.CorrectAnswer,
            Explanation = currentQuestion.Explanation,
            TestComplete = false
        };
    }

    public async Task<PlacementTestResultViewModel?> GetResultsAsync(int sessionId)
    {
        var session = await GetSessionByIdAsync(sessionId);
        if (session == null || session.Status != PlacementTestStatus.Completed)
        {
            return null;
        }

        var levelBreakdown = ParseLevelProgress(session.LevelProgressJson);

        var result = new PlacementTestResultViewModel
        {
            SessionId = sessionId,
            DeterminedLevel = session.DeterminedLevel ?? "A1",
            TotalQuestions = session.TotalQuestionsAnswered,
            TotalCorrect = session.TotalCorrect,
            TimeTaken = (session.CompletedAt ?? DateTime.UtcNow) - session.StartedAt,
            LevelBreakdown = levelBreakdown
        };

        result.LevelDescription = result.GetLevelDescription(result.DeterminedLevel);

        return result;
    }

    public async Task<PlacementTestSession?> GetActiveSessionAsync(string studentId)
    {
        return await _context.PlacementTestSessions
            .FirstOrDefaultAsync(s => s.StudentId == studentId
                                   && s.Status == PlacementTestStatus.InProgress);
    }

    public async Task<PlacementTestSession?> GetSessionByIdAsync(int sessionId)
    {
        return await _context.PlacementTestSessions.FindAsync(sessionId);
    }

    public async Task<bool> AbandonTestAsync(int sessionId)
    {
        var session = await GetSessionByIdAsync(sessionId);
        if (session == null || session.Status != PlacementTestStatus.InProgress)
        {
            return false;
        }

        session.Status = PlacementTestStatus.Abandoned;
        session.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Abandoned placement test session {SessionId}", sessionId);
        return true;
    }

    public async Task<bool> HasCompletedTestAsync(string studentId)
    {
        return await _context.PlacementTestSessions
            .AnyAsync(s => s.StudentId == studentId
                        && s.Status == PlacementTestStatus.Completed);
    }

    public async Task<PlacementTestSession?> GetLastCompletedTestAsync(string studentId)
    {
        return await _context.PlacementTestSessions
            .Where(s => s.StudentId == studentId && s.Status == PlacementTestStatus.Completed)
            .OrderByDescending(s => s.CompletedAt)
            .FirstOrDefaultAsync();
    }

    // Private helper methods

    private List<string> GetPreviousTopics(PlacementTestSession session)
    {
        try
        {
            using var doc = JsonDocument.Parse(session.QuestionsJson);
            return doc.RootElement.EnumerateArray()
                .Select(q => q.TryGetProperty("topic", out var t) ? t.GetString() : null)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList()!;
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task StoreCurrentQuestionAsync(PlacementTestSession session, PlacementQuestion question)
    {
        // Store current question temporarily for grading
        // We use a simple approach: add to questions array with "pending" status
        var questions = ParseQuestionsJson(session.QuestionsJson);
        questions.Add(new QuestionRecord
        {
            Level = question.Level,
            Type = question.Type,
            Question = question.Question,
            CorrectAnswer = question.CorrectAnswer,
            AcceptableAnswers = question.AcceptableAnswers,
            Explanation = question.Explanation,
            Topic = question.Topic,
            Pending = true
        });

        session.QuestionsJson = JsonSerializer.Serialize(questions);
        await _context.SaveChangesAsync();
    }

    private PlacementQuestion? GetCurrentQuestion(PlacementTestSession session)
    {
        var questions = ParseQuestionsJson(session.QuestionsJson);
        var pending = questions.LastOrDefault(q => q.Pending);

        if (pending == null) return null;

        return new PlacementQuestion
        {
            Level = pending.Level,
            Type = pending.Type,
            Question = pending.Question,
            CorrectAnswer = pending.CorrectAnswer,
            AcceptableAnswers = pending.AcceptableAnswers,
            Explanation = pending.Explanation,
            Topic = pending.Topic,
            Success = true
        };
    }

    private bool GradeAnswer(string studentAnswer, PlacementQuestion question)
    {
        var normalizedAnswer = studentAnswer?.Trim().ToLowerInvariant() ?? "";
        var correctAnswer = question.CorrectAnswer?.Trim().ToLowerInvariant() ?? "";

        if (normalizedAnswer == correctAnswer)
            return true;

        // Check acceptable answers
        if (question.AcceptableAnswers != null)
        {
            foreach (var acceptable in question.AcceptableAnswers)
            {
                if (normalizedAnswer == acceptable?.Trim().ToLowerInvariant())
                    return true;
            }
        }

        return false;
    }

    private void UpdateLevelProgress(PlacementTestSession session, bool isCorrect)
    {
        var progress = ParseLevelProgress(session.LevelProgressJson);

        if (!progress.ContainsKey(session.CurrentLevel))
        {
            progress[session.CurrentLevel] = new LevelPerformance { Correct = 0, Total = 0 };
        }

        progress[session.CurrentLevel].Total++;
        if (isCorrect)
        {
            progress[session.CurrentLevel].Correct++;
        }

        session.LevelProgressJson = JsonSerializer.Serialize(progress);
    }

    private async Task RecordAnswerAsync(PlacementTestSession session, PlacementQuestion question, string answer, bool isCorrect)
    {
        var questions = ParseQuestionsJson(session.QuestionsJson);
        var pending = questions.LastOrDefault(q => q.Pending);

        if (pending != null)
        {
            pending.Pending = false;
            pending.StudentAnswer = answer;
            pending.IsCorrect = isCorrect;
        }

        session.QuestionsJson = JsonSerializer.Serialize(questions);
    }

    private (bool shouldContinue, string newLevel) EvaluateProgress(PlacementTestSession session)
    {
        var progress = ParseLevelProgress(session.LevelProgressJson);
        var currentLevelIndex = Array.IndexOf(CefrLevels, session.CurrentLevel);

        if (!progress.TryGetValue(session.CurrentLevel, out var currentProgress))
        {
            return (true, session.CurrentLevel);
        }

        // Only evaluate after completing questions for this level
        if (currentProgress.Total < QuestionsPerLevel)
        {
            return (true, session.CurrentLevel);
        }

        var percentage = currentProgress.Percentage / 100m;

        // High score - try to advance
        if (percentage >= AdvanceThreshold)
        {
            if (currentLevelIndex >= CefrLevels.Length - 1)
            {
                // Already at C2, test complete
                return (false, session.CurrentLevel);
            }

            // Reset failed advance count on success
            session.FailedAdvanceCount = 0;
            return (true, CefrLevels[currentLevelIndex + 1]);
        }

        // Low score - drop level or end test
        if (percentage < DropThreshold)
        {
            session.FailedAdvanceCount++;

            if (currentLevelIndex <= 0 || session.FailedAdvanceCount >= MaxFailedAdvances)
            {
                // At A1 or failed too many times - end test
                return (false, session.CurrentLevel);
            }

            return (true, CefrLevels[currentLevelIndex - 1]);
        }

        // Middle score - failed to advance but not dropping
        session.FailedAdvanceCount++;

        if (session.FailedAdvanceCount >= MaxFailedAdvances)
        {
            // Level has stabilized
            return (false, session.CurrentLevel);
        }

        // Continue at same level
        return (true, session.CurrentLevel);
    }

    private async Task FinalizeTestAsync(PlacementTestSession session)
    {
        // Determine final level based on performance
        var progress = ParseLevelProgress(session.LevelProgressJson);
        string determinedLevel = "A1";

        // Find highest level with acceptable performance
        foreach (var level in CefrLevels)
        {
            if (progress.TryGetValue(level, out var perf) && perf.Total > 0)
            {
                var pct = perf.Percentage / 100m;
                if (pct >= DropThreshold)
                {
                    determinedLevel = level;
                }
            }
        }

        session.DeterminedLevel = determinedLevel;
        session.Status = PlacementTestStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;

        // Update student's CEFR level
        var student = await _userManager.FindByIdAsync(session.StudentId);
        if (student != null)
        {
            student.CefrLevel = determinedLevel;
            await _userManager.UpdateAsync(student);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Completed placement test session {SessionId} with level {Level}",
            session.Id, determinedLevel);
    }

    private PlacementQuestion GetFallbackQuestion(string cefrLevel)
    {
        // Fallback questions if API fails
        return cefrLevel switch
        {
            "A1" => new PlacementQuestion
            {
                Level = "A1",
                Type = "multiple_choice",
                Question = "How do you say 'hello' in Spanish?",
                Options = new List<string> { "Hola", "Adiós", "Gracias", "Por favor" },
                CorrectAnswer = "Hola",
                Explanation = "'Hola' is the Spanish word for 'hello'.",
                Topic = "basic greetings",
                Success = true
            },
            "A2" => new PlacementQuestion
            {
                Level = "A2",
                Type = "multiple_choice",
                Question = "Complete: Yo _____ al supermercado ayer. (I went to the supermarket yesterday)",
                Options = new List<string> { "fui", "voy", "iré", "iba" },
                CorrectAnswer = "fui",
                Explanation = "'Fui' is the first person singular preterite of 'ir'.",
                Topic = "past tense",
                Success = true
            },
            _ => new PlacementQuestion
            {
                Level = cefrLevel,
                Type = "multiple_choice",
                Question = "Select the correct form: Ella _____ estudiante. (She is a student)",
                Options = new List<string> { "es", "está", "ser", "estar" },
                CorrectAnswer = "es",
                Explanation = "'Es' is used for permanent characteristics like profession.",
                Topic = "ser vs estar",
                Success = true
            }
        };
    }

    private List<QuestionRecord> ParseQuestionsJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<QuestionRecord>>(json) ?? new List<QuestionRecord>();
        }
        catch
        {
            return new List<QuestionRecord>();
        }
    }

    private Dictionary<string, LevelPerformance> ParseLevelProgress(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, LevelPerformance>>(json)
                ?? new Dictionary<string, LevelPerformance>();
        }
        catch
        {
            return new Dictionary<string, LevelPerformance>();
        }
    }

    private class QuestionRecord
    {
        public string Level { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public List<string>? AcceptableAnswers { get; set; }
        public string? Explanation { get; set; }
        public string? Topic { get; set; }
        public string? StudentAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public bool Pending { get; set; }
    }
}
