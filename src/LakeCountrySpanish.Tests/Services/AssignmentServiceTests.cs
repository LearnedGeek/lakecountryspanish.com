using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Tests.Services;

/// <summary>
/// Tests for assignment-related data operations.
/// </summary>
public class AssignmentServiceTests
{
    private readonly ApplicationDbContext _context;

    public AssignmentServiceTests()
    {
        _context = TestDbContextFactory.Create();
    }

    [Fact]
    public async Task Assignment_CanBeCreated()
    {
        // Arrange
        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);
        await _context.SaveChangesAsync();

        var assignment = new Assignment
        {
            Title = "Test Assignment",
            Description = "A test assignment",
            CefrLevel = "A1",
            CurriculumTopicId = topic.Id,
            Type = AssignmentType.MultipleChoice,
            Status = AssignmentStatus.Approved,
            EstimatedMinutes = 15,
            TotalPoints = 100
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.Assignments
            .Include(a => a.CurriculumTopic)
            .FirstOrDefaultAsync(a => a.Id == assignment.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Assignment", result.Title);
        Assert.Equal(AssignmentType.MultipleChoice, result.Type);
    }

    [Fact]
    public async Task StudentAssignment_CanBeAssigned()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);

        var assignment = new Assignment
        {
            Title = "Homework 1",
            CefrLevel = "A1",
            CurriculumTopicId = topic.Id,
            Type = AssignmentType.FillInTheBlank,
            Status = AssignmentStatus.Approved,
            EstimatedMinutes = 20,
            TotalPoints = 50
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        var studentAssignment = new StudentAssignment
        {
            StudentId = student.Id,
            AssignmentId = assignment.Id,
            AssignedAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7),
            Status = StudentAssignmentStatus.Assigned
        };
        _context.StudentAssignments.Add(studentAssignment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.StudentAssignments
            .Include(sa => sa.Assignment)
            .FirstOrDefaultAsync(sa => sa.Id == studentAssignment.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(StudentAssignmentStatus.Assigned, result.Status);
        Assert.Equal("Homework 1", result.Assignment?.Title);
    }

    [Fact]
    public async Task StudentAssignment_StatusProgression()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);

        var assignment = new Assignment
        {
            Title = "Quiz 1",
            CefrLevel = "A2",
            CurriculumTopicId = topic.Id,
            Type = AssignmentType.MultipleChoice,
            Status = AssignmentStatus.Approved,
            EstimatedMinutes = 30,
            TotalPoints = 100
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        var studentAssignment = new StudentAssignment
        {
            StudentId = student.Id,
            AssignmentId = assignment.Id,
            AssignedAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7),
            Status = StudentAssignmentStatus.Assigned
        };
        _context.StudentAssignments.Add(studentAssignment);
        await _context.SaveChangesAsync();

        // Act - Progress through statuses
        studentAssignment.Status = StudentAssignmentStatus.InProgress;
        studentAssignment.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var inProgress = await _context.StudentAssignments.FindAsync(studentAssignment.Id);
        Assert.Equal(StudentAssignmentStatus.InProgress, inProgress?.Status);

        studentAssignment.Status = StudentAssignmentStatus.Completed;
        studentAssignment.CompletedAt = DateTime.UtcNow;
        studentAssignment.BestScore = 85;
        await _context.SaveChangesAsync();

        var completed = await _context.StudentAssignments.FindAsync(studentAssignment.Id);
        Assert.Equal(StudentAssignmentStatus.Completed, completed?.Status);
        Assert.Equal(85, completed?.BestScore);
    }

    [Fact]
    public async Task StudentAssignment_OnlyStudentAssignmentsReturned()
    {
        // Arrange
        var student1 = CreateTestStudent();
        var student2 = CreateTestStudent();
        _context.Users.AddRange(student1, student2);

        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);

        var assignment = new Assignment
        {
            Title = "Shared Assignment",
            CefrLevel = "A1",
            CurriculumTopicId = topic.Id,
            Type = AssignmentType.FillInTheBlank,
            Status = AssignmentStatus.Approved,
            EstimatedMinutes = 20
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        var student1Assignment = new StudentAssignment
        {
            StudentId = student1.Id,
            AssignmentId = assignment.Id,
            DueDate = DateTime.UtcNow.AddDays(7),
            Status = StudentAssignmentStatus.Assigned
        };
        var student2Assignment = new StudentAssignment
        {
            StudentId = student2.Id,
            AssignmentId = assignment.Id,
            DueDate = DateTime.UtcNow.AddDays(7),
            Status = StudentAssignmentStatus.Assigned
        };
        _context.StudentAssignments.AddRange(student1Assignment, student2Assignment);
        await _context.SaveChangesAsync();

        // Act
        var student1Assignments = await _context.StudentAssignments
            .Where(sa => sa.StudentId == student1.Id)
            .ToListAsync();

        // Assert
        Assert.Single(student1Assignments);
        Assert.Equal(student1.Id, student1Assignments.First().StudentId);
    }

    [Fact]
    public async Task StudentAssignment_PendingCount()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);

        var assignment = new Assignment
        {
            Title = "Count Test",
            CefrLevel = "A1",
            CurriculumTopicId = topic.Id,
            Type = AssignmentType.FillInTheBlank,
            Status = AssignmentStatus.Approved,
            EstimatedMinutes = 15
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        var studentAssignments = new List<StudentAssignment>
        {
            new() { StudentId = student.Id, AssignmentId = assignment.Id, DueDate = DateTime.UtcNow.AddDays(1), Status = StudentAssignmentStatus.Assigned },
            new() { StudentId = student.Id, AssignmentId = assignment.Id, DueDate = DateTime.UtcNow.AddDays(2), Status = StudentAssignmentStatus.InProgress },
            new() { StudentId = student.Id, AssignmentId = assignment.Id, DueDate = DateTime.UtcNow.AddDays(3), Status = StudentAssignmentStatus.Assigned },
            new() { StudentId = student.Id, AssignmentId = assignment.Id, DueDate = DateTime.UtcNow.AddDays(-1), Status = StudentAssignmentStatus.Completed }
        };
        _context.StudentAssignments.AddRange(studentAssignments);
        await _context.SaveChangesAsync();

        // Act
        var pendingCount = await _context.StudentAssignments
            .CountAsync(sa => sa.StudentId == student.Id &&
                (sa.Status == StudentAssignmentStatus.Assigned || sa.Status == StudentAssignmentStatus.InProgress));

        // Assert
        Assert.Equal(3, pendingCount);
    }

    [Fact]
    public async Task StudentAssignment_OverdueDetection()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);

        var assignment = new Assignment
        {
            Title = "Overdue Test",
            CefrLevel = "A1",
            CurriculumTopicId = topic.Id,
            Type = AssignmentType.FillInTheBlank,
            Status = AssignmentStatus.Approved,
            EstimatedMinutes = 10
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        var studentAssignments = new List<StudentAssignment>
        {
            new() { StudentId = student.Id, AssignmentId = assignment.Id, DueDate = DateTime.UtcNow.AddDays(-5), Status = StudentAssignmentStatus.Assigned }, // Overdue
            new() { StudentId = student.Id, AssignmentId = assignment.Id, DueDate = DateTime.UtcNow.AddDays(-3), Status = StudentAssignmentStatus.Completed }, // Past but completed
            new() { StudentId = student.Id, AssignmentId = assignment.Id, DueDate = DateTime.UtcNow.AddDays(5), Status = StudentAssignmentStatus.Assigned } // Future
        };
        _context.StudentAssignments.AddRange(studentAssignments);
        await _context.SaveChangesAsync();

        // Act
        var overdue = await _context.StudentAssignments
            .Where(sa => sa.StudentId == student.Id &&
                sa.DueDate < DateTime.UtcNow &&
                sa.Status != StudentAssignmentStatus.Completed)
            .ToListAsync();

        // Assert
        Assert.Single(overdue);
    }

    [Fact]
    public async Task AssignmentSubmission_DifficultyFeedback()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);

        var assignment = new Assignment
        {
            Title = "Feedback Test",
            CefrLevel = "B1",
            CurriculumTopicId = topic.Id,
            Type = AssignmentType.MultipleChoice,
            Status = AssignmentStatus.Approved,
            EstimatedMinutes = 15
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        var submission = new AssignmentSubmission
        {
            AssignmentId = assignment.Id,
            StudentId = student.Id,
            CorrectAnswers = 8,
            TotalQuestions = 10,
            PercentageScore = 80,
            PointsEarned = 80,
            TimeTakenSeconds = 600,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            SubmittedAt = DateTime.UtcNow
        };
        _context.AssignmentSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        // Act
        submission.DifficultyFeedback = DifficultyFeedback.TooHard;
        await _context.SaveChangesAsync();

        var result = await _context.AssignmentSubmissions.FindAsync(submission.Id);

        // Assert
        Assert.Equal(DifficultyFeedback.TooHard, result?.DifficultyFeedback);
    }

    [Fact]
    public async Task CurriculumTopic_OnlyActiveReturned()
    {
        // Arrange
        var activeTopic = new CurriculumTopic
        {
            Name = "Active Topic",
            Description = "An active topic",
            CefrLevel = "A1",
            Type = TopicType.Grammar,
            DisplayOrder = 1,
            IsActive = true
        };
        var inactiveTopic = new CurriculumTopic
        {
            Name = "Inactive Topic",
            Description = "An inactive topic",
            CefrLevel = "A2",
            Type = TopicType.Grammar,
            DisplayOrder = 2,
            IsActive = false
        };
        _context.CurriculumTopics.AddRange(activeTopic, inactiveTopic);
        await _context.SaveChangesAsync();

        // Act
        var activeTopics = await _context.CurriculumTopics
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();

        // Assert
        Assert.Single(activeTopics);
        Assert.Equal("Active Topic", activeTopics.First().Name);
    }

    [Fact]
    public async Task Assignment_DifferentTypes()
    {
        // Arrange
        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);
        await _context.SaveChangesAsync();

        var assignments = new List<Assignment>
        {
            new() { Title = "Fill In", CefrLevel = "A1", CurriculumTopicId = topic.Id, Type = AssignmentType.FillInTheBlank, Status = AssignmentStatus.Approved },
            new() { Title = "Quiz", CefrLevel = "A1", CurriculumTopicId = topic.Id, Type = AssignmentType.MultipleChoice, Status = AssignmentStatus.Approved },
            new() { Title = "Translate", CefrLevel = "A1", CurriculumTopicId = topic.Id, Type = AssignmentType.Translation, Status = AssignmentStatus.Approved },
            new() { Title = "Match", CefrLevel = "A1", CurriculumTopicId = topic.Id, Type = AssignmentType.Matching, Status = AssignmentStatus.Approved }
        };
        _context.Assignments.AddRange(assignments);
        await _context.SaveChangesAsync();

        // Act
        var types = await _context.Assignments
            .Select(a => a.Type)
            .Distinct()
            .ToListAsync();

        // Assert
        Assert.Equal(4, types.Count);
    }

    [Fact]
    public async Task Assignment_DifferentStatuses()
    {
        // Arrange
        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);
        await _context.SaveChangesAsync();

        var assignments = new List<Assignment>
        {
            new() { Title = "Draft", CefrLevel = "A1", CurriculumTopicId = topic.Id, Type = AssignmentType.MultipleChoice, Status = AssignmentStatus.Draft },
            new() { Title = "Pending", CefrLevel = "B1", CurriculumTopicId = topic.Id, Type = AssignmentType.MultipleChoice, Status = AssignmentStatus.PendingReview },
            new() { Title = "Approved", CefrLevel = "C1", CurriculumTopicId = topic.Id, Type = AssignmentType.MultipleChoice, Status = AssignmentStatus.Approved }
        };
        _context.Assignments.AddRange(assignments);
        await _context.SaveChangesAsync();

        // Act
        var statuses = await _context.Assignments
            .Select(a => a.Status)
            .Distinct()
            .ToListAsync();

        // Assert
        Assert.Equal(3, statuses.Count);
    }

    [Fact]
    public async Task AssignmentSubmission_RecordsStudentWork()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var topic = CreateTestTopic();
        _context.CurriculumTopics.Add(topic);

        var assignment = new Assignment
        {
            Title = "Submission Test",
            CefrLevel = "A1",
            CurriculumTopicId = topic.Id,
            Type = AssignmentType.FillInTheBlank,
            Status = AssignmentStatus.Approved,
            EstimatedMinutes = 15,
            TotalPoints = 100
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        var submission = new AssignmentSubmission
        {
            AssignmentId = assignment.Id,
            StudentId = student.Id,
            AnswersJson = "[{\"id\":1,\"answer\":\"hola\"}]",
            CorrectAnswers = 1,
            TotalQuestions = 1,
            PercentageScore = 100,
            PointsEarned = 100,
            TimeTakenSeconds = 120,
            IsFirstAttempt = true,
            AttemptNumber = 1,
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            SubmittedAt = DateTime.UtcNow
        };
        _context.AssignmentSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .FirstOrDefaultAsync(s => s.AssignmentId == assignment.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("hola", result.AnswersJson);
        Assert.True(result.IsFirstAttempt);
    }

    private ApplicationUser CreateTestStudent()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"test{Guid.NewGuid():N}@test.com",
            Email = $"test{Guid.NewGuid():N}@test.com",
            FirstName = "Test",
            LastName = "Student"
        };
    }

    private CurriculumTopic CreateTestTopic()
    {
        return new CurriculumTopic
        {
            Name = $"Test Topic {Guid.NewGuid():N}",
            Description = "A test topic",
            CefrLevel = "A1",
            Type = TopicType.Grammar,
            DisplayOrder = 1,
            IsActive = true
        };
    }
}
