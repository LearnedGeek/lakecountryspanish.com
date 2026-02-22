using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

public class PlacementTestServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IClaudeApiService> _claudeApiMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<PlacementTestService>> _loggerMock;
    private readonly PlacementTestService _service;
    private readonly string _studentId;

    public PlacementTestServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _claudeApiMock = new Mock<IClaudeApiService>();
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _loggerMock = new Mock<ILogger<PlacementTestService>>();

        _service = new PlacementTestService(
            _context,
            _claudeApiMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object);

        _studentId = Guid.NewGuid().ToString();
        _context.Users.Add(new ApplicationUser
        {
            Id = _studentId,
            UserName = "student@test.com",
            Email = "student@test.com",
            FirstName = "Test",
            LastName = "Student"
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region StartTestAsync Tests

    [Fact]
    public async Task StartTest_CreatesNewSession()
    {
        var session = await _service.StartTestAsync(_studentId);

        Assert.NotNull(session);
        Assert.Equal(_studentId, session.StudentId);
        Assert.Equal(PlacementTestStatus.InProgress, session.Status);
        Assert.Equal("A2", session.CurrentLevel);
        Assert.Single(_context.PlacementTestSessions);
    }

    [Fact]
    public async Task StartTest_ReturnsExistingActiveSession()
    {
        var existing = new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.InProgress,
            CurrentLevel = "A2",
            StartingLevel = "A2"
        };
        _context.PlacementTestSessions.Add(existing);
        await _context.SaveChangesAsync();

        var session = await _service.StartTestAsync(_studentId);

        Assert.NotNull(session);
        Assert.Equal(existing.Id, session.Id);
        Assert.Single(_context.PlacementTestSessions);
    }

    #endregion

    #region GetActiveSessionAsync Tests

    [Fact]
    public async Task GetActiveSession_ReturnsActiveSession()
    {
        _context.PlacementTestSessions.Add(new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.InProgress,
            CurrentLevel = "A2",
            StartingLevel = "A2"
        });
        await _context.SaveChangesAsync();

        var session = await _service.GetActiveSessionAsync(_studentId);

        Assert.NotNull(session);
        Assert.Equal(PlacementTestStatus.InProgress, session.Status);
    }

    [Fact]
    public async Task GetActiveSession_ReturnsNull_WhenNoActive()
    {
        _context.PlacementTestSessions.Add(new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.Completed,
            CurrentLevel = "B1",
            StartingLevel = "A2",
            DeterminedLevel = "B1",
            CompletedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var session = await _service.GetActiveSessionAsync(_studentId);

        Assert.Null(session);
    }

    #endregion

    #region GetSessionByIdAsync Tests

    [Fact]
    public async Task GetSessionById_ReturnsSession()
    {
        var created = new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.InProgress,
            CurrentLevel = "A2",
            StartingLevel = "A2"
        };
        _context.PlacementTestSessions.Add(created);
        await _context.SaveChangesAsync();

        var session = await _service.GetSessionByIdAsync(created.Id);

        Assert.NotNull(session);
        Assert.Equal(created.Id, session.Id);
    }

    [Fact]
    public async Task GetSessionById_ReturnsNull_WhenNotFound()
    {
        var session = await _service.GetSessionByIdAsync(999);

        Assert.Null(session);
    }

    #endregion

    #region AbandonTestAsync Tests

    [Fact]
    public async Task AbandonTest_MarksSessionAsAbandoned()
    {
        var created = new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.InProgress,
            CurrentLevel = "A2",
            StartingLevel = "A2"
        };
        _context.PlacementTestSessions.Add(created);
        await _context.SaveChangesAsync();

        var result = await _service.AbandonTestAsync(created.Id);

        Assert.True(result);
        var session = await _context.PlacementTestSessions.FindAsync(created.Id);
        Assert.Equal(PlacementTestStatus.Abandoned, session!.Status);
    }

    [Fact]
    public async Task AbandonTest_ReturnsFalse_WhenSessionNotFound()
    {
        var result = await _service.AbandonTestAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task AbandonTest_ReturnsFalse_WhenAlreadyCompleted()
    {
        var created = new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.Completed,
            CurrentLevel = "B1",
            StartingLevel = "A2",
            DeterminedLevel = "B1",
            CompletedAt = DateTime.UtcNow
        };
        _context.PlacementTestSessions.Add(created);
        await _context.SaveChangesAsync();

        var result = await _service.AbandonTestAsync(created.Id);

        Assert.False(result);
    }

    #endregion

    #region HasCompletedTestAsync Tests

    [Fact]
    public async Task HasCompletedTest_ReturnsTrue_WhenCompleted()
    {
        _context.PlacementTestSessions.Add(new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.Completed,
            CurrentLevel = "B1",
            StartingLevel = "A2",
            DeterminedLevel = "B1",
            CompletedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _service.HasCompletedTestAsync(_studentId);

        Assert.True(result);
    }

    [Fact]
    public async Task HasCompletedTest_ReturnsFalse_WhenNotCompleted()
    {
        var result = await _service.HasCompletedTestAsync(_studentId);

        Assert.False(result);
    }

    [Fact]
    public async Task HasCompletedTest_ReturnsFalse_WhenOnlyAbandoned()
    {
        _context.PlacementTestSessions.Add(new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.Abandoned,
            CurrentLevel = "A2",
            StartingLevel = "A2"
        });
        await _context.SaveChangesAsync();

        var result = await _service.HasCompletedTestAsync(_studentId);

        Assert.False(result);
    }

    #endregion

    #region GetLastCompletedTestAsync Tests

    [Fact]
    public async Task GetLastCompletedTest_ReturnsLatestCompletedSession()
    {
        _context.PlacementTestSessions.Add(new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.Completed,
            CurrentLevel = "A2",
            StartingLevel = "A2",
            DeterminedLevel = "A2",
            CompletedAt = DateTime.UtcNow.AddDays(-10)
        });
        _context.PlacementTestSessions.Add(new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.Completed,
            CurrentLevel = "B1",
            StartingLevel = "A2",
            DeterminedLevel = "B1",
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var session = await _service.GetLastCompletedTestAsync(_studentId);

        Assert.NotNull(session);
        Assert.Equal("B1", session.DeterminedLevel);
    }

    [Fact]
    public async Task GetLastCompletedTest_ReturnsNull_WhenNoneCompleted()
    {
        var session = await _service.GetLastCompletedTestAsync(_studentId);

        Assert.Null(session);
    }

    #endregion

    #region GetResultsAsync Tests

    [Fact]
    public async Task GetResults_ReturnsNull_WhenSessionNotCompleted()
    {
        var created = new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.InProgress,
            CurrentLevel = "A2",
            StartingLevel = "A2"
        };
        _context.PlacementTestSessions.Add(created);
        await _context.SaveChangesAsync();

        var result = await _service.GetResultsAsync(created.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetResults_ReturnsResults_WhenSessionCompleted()
    {
        var created = new PlacementTestSession
        {
            StudentId = _studentId,
            Status = PlacementTestStatus.Completed,
            CurrentLevel = "B1",
            StartingLevel = "A2",
            DeterminedLevel = "B1",
            TotalQuestionsAnswered = 10,
            TotalCorrect = 8,
            CompletedAt = DateTime.UtcNow,
            QuestionsJson = "[]",
            LevelProgressJson = "{}"
        };
        _context.PlacementTestSessions.Add(created);
        await _context.SaveChangesAsync();

        var result = await _service.GetResultsAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("B1", result.DeterminedLevel);
    }

    #endregion
}
