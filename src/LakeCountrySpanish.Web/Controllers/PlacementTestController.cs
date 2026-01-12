using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = "Student")]
public class PlacementTestController : Controller
{
    private readonly IPlacementTestService _placementTestService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlacementTestController(
        IPlacementTestService placementTestService,
        UserManager<ApplicationUser> userManager)
    {
        _placementTestService = placementTestService;
        _userManager = userManager;
    }

    /// <summary>
    /// Placement test introduction page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var activeSession = await _placementTestService.GetActiveSessionAsync(user.Id);
        var lastCompleted = await _placementTestService.GetLastCompletedTestAsync(user.Id);

        var viewModel = new PlacementTestIndexViewModel
        {
            HasActiveSession = activeSession != null,
            ActiveSessionId = activeSession?.Id,
            HasCompletedTest = lastCompleted != null,
            CurrentLevel = user.CefrLevel,
            LastTestDate = lastCompleted?.CompletedAt
        };

        return View(viewModel);
    }

    /// <summary>
    /// Start a new placement test.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var session = await _placementTestService.StartTestAsync(user.Id);

        return RedirectToAction(nameof(Question), new { sessionId = session.Id });
    }

    /// <summary>
    /// Display the current question.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Question(int sessionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var session = await _placementTestService.GetSessionByIdAsync(sessionId);
        if (session == null || session.StudentId != user.Id)
        {
            return NotFound();
        }

        if (session.Status == PlacementTestStatus.Completed)
        {
            return RedirectToAction(nameof(Results), new { sessionId });
        }

        if (session.Status == PlacementTestStatus.Abandoned)
        {
            TempData["ErrorMessage"] = "This test session was abandoned.";
            return RedirectToAction(nameof(Index));
        }

        var question = await _placementTestService.GetNextQuestionAsync(sessionId);

        if (question == null)
        {
            // Test is complete
            return RedirectToAction(nameof(Results), new { sessionId });
        }

        return View(question);
    }

    /// <summary>
    /// Submit an answer.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(PlacementTestSubmitViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var session = await _placementTestService.GetSessionByIdAsync(model.SessionId);
        if (session == null || session.StudentId != user.Id)
        {
            return NotFound();
        }

        var feedback = await _placementTestService.SubmitAnswerAsync(model.SessionId, model.Answer);

        if (feedback.TestComplete)
        {
            TempData["TestComplete"] = true;
            TempData["DeterminedLevel"] = feedback.DeterminedLevel;
            return RedirectToAction(nameof(Results), new { sessionId = model.SessionId });
        }

        // Store feedback temporarily for display
        TempData["LastAnswerCorrect"] = feedback.IsCorrect;
        TempData["LastCorrectAnswer"] = feedback.CorrectAnswer;
        TempData["LastExplanation"] = feedback.Explanation;

        return RedirectToAction(nameof(Question), new { sessionId = model.SessionId });
    }

    /// <summary>
    /// Display test results.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Results(int sessionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var session = await _placementTestService.GetSessionByIdAsync(sessionId);
        if (session == null || session.StudentId != user.Id)
        {
            return NotFound();
        }

        if (session.Status != PlacementTestStatus.Completed)
        {
            return RedirectToAction(nameof(Question), new { sessionId });
        }

        var results = await _placementTestService.GetResultsAsync(sessionId);
        if (results == null)
        {
            return NotFound();
        }

        return View(results);
    }

    /// <summary>
    /// Abandon the current test.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Abandon(int sessionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var session = await _placementTestService.GetSessionByIdAsync(sessionId);
        if (session == null || session.StudentId != user.Id)
        {
            return NotFound();
        }

        await _placementTestService.AbandonTestAsync(sessionId);

        TempData["InfoMessage"] = "Placement test abandoned. You can start a new one anytime.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Resume an existing in-progress test.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Resume()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var activeSession = await _placementTestService.GetActiveSessionAsync(user.Id);
        if (activeSession == null)
        {
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Question), new { sessionId = activeSession.Id });
    }
}
