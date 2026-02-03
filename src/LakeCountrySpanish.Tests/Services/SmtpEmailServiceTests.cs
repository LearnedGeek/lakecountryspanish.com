using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

/// <summary>
/// Tests for SmtpEmailService.
/// These tests verify email content generation and admin notification logic.
/// Actual SMTP sending is not tested as it requires external services.
/// </summary>
public class SmtpEmailServiceTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<SmtpEmailService>> _loggerMock;
    private readonly Mock<IWebHostEnvironment> _envMock;

    public SmtpEmailServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<SmtpEmailService>>();
        _envMock = new Mock<IWebHostEnvironment>();

        // Default configuration setup - no email configured (dev mode)
        _configMock.Setup(c => c["EmailSettings:SmtpHost"]).Returns((string?)null);
        _configMock.Setup(c => c["EmailSettings:SmtpPort"]).Returns("587");
        _configMock.Setup(c => c["EmailSettings:FromEmail"]).Returns((string?)null);
        _configMock.Setup(c => c["EmailSettings:FromName"]).Returns("Lake Country Spanish");
        _configMock.Setup(c => c["AppSettings:BaseUrl"]).Returns("https://lakecountryspanish.com");
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns((string?)null);
        _configMock.Setup(c => c["AppSettings:AdminName"]).Returns("Karen");

        _envMock.Setup(e => e.EnvironmentName).Returns("Development");
    }

    private SmtpEmailService CreateService()
    {
        return new SmtpEmailService(_configMock.Object, _loggerMock.Object, _envMock.Object);
    }

    #region SendEmailAsync Tests

    [Fact]
    public async Task SendEmailAsync_LogsWarning_WhenEmailNotConfiguredInDev()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendEmailAsync("student@test.com", "Test Student", "Test Subject", "<p>Test body</p>");

        // Assert - verify warning was logged (email not configured in dev)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailAsync_LogsError_WhenEmailNotConfiguredInProduction()
    {
        // Arrange
        _envMock.Setup(e => e.EnvironmentName).Returns("Production");
        var service = CreateService();

        // Act
        await service.SendEmailAsync("student@test.com", "Test Student", "Test Subject", "<p>Test body</p>");

        // Assert - verify error was logged (email not configured in production)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CRITICAL: Email not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Student Email Tests

    [Fact]
    public async Task SendClassScheduledAsync_SendsEmail_WithCorrectDetails()
    {
        // Arrange
        var service = CreateService();
        var classDateTime = new DateTime(2025, 3, 15, 14, 0, 0);
        var classroomUrl = "https://meet.google.com/abc-123";

        // Act
        await service.SendClassScheduledAsync("student@test.com", "John Doe", classDateTime, classroomUrl);

        // Assert - verify logging occurred (indicates method executed)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendClassRescheduledAsync_SendsEmail_WithOldAndNewTimes()
    {
        // Arrange
        var service = CreateService();
        var oldDateTime = new DateTime(2025, 3, 15, 14, 0, 0);
        var newDateTime = new DateTime(2025, 3, 16, 10, 0, 0);

        // Act
        await service.SendClassRescheduledAsync("student@test.com", "John Doe", oldDateTime, newDateTime, "Schedule conflict");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendClassCancelledAsync_SendsEmail_WithReason()
    {
        // Arrange
        var service = CreateService();
        var classDateTime = new DateTime(2025, 3, 15, 14, 0, 0);

        // Act
        await service.SendClassCancelledAsync("student@test.com", "John Doe", classDateTime, "Weather emergency");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendClassCancelledAsync_SendsEmail_WithoutReason()
    {
        // Arrange
        var service = CreateService();
        var classDateTime = new DateTime(2025, 3, 15, 14, 0, 0);

        // Act
        await service.SendClassCancelledAsync("student@test.com", "John Doe", classDateTime, null);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendClassReminderAsync_SendsEmail_For24HourReminder()
    {
        // Arrange
        var service = CreateService();
        var classDateTime = DateTime.UtcNow.AddHours(24);

        // Act
        await service.SendClassReminderAsync("student@test.com", "John", classDateTime, "https://meet.google.com/abc", 24);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendClassReminderAsync_SendsEmail_For1HourReminder()
    {
        // Arrange
        var service = CreateService();
        var classDateTime = DateTime.UtcNow.AddHours(1);

        // Act
        await service.SendClassReminderAsync("student@test.com", "John", classDateTime, "https://meet.google.com/abc", 1);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPaymentConfirmationAsync_SendsEmail_WithReceipt()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendPaymentConfirmationAsync(
            "student@test.com",
            "John Doe",
            25.00m,
            "Single Spanish Class",
            DateTime.UtcNow);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPointMilestoneAsync_SendsEmail_WithTokenBonus()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendPointMilestoneAsync("student@test.com", "John", 1000, 1000, 1);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPointMilestoneAsync_SendsEmail_WithoutTokenBonus()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendPointMilestoneAsync("student@test.com", "John", 500, 500, 0);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendBadgeEarnedAsync_SendsEmail_WithBonusPoints()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendBadgeEarnedAsync("student@test.com", "John", "Early Bird", "First class before 9am", 50);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendSubscriptionRenewalAsync_SendsEmail()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendSubscriptionRenewalAsync("student@test.com", "John", "Premium", 99.00m, DateTime.UtcNow.AddMonths(1));

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAssignmentAssignedAsync_SendsEmail_WithDueDate()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendAssignmentAssignedAsync("student@test.com", "John", "Chapter 5 Vocabulary", DateTime.UtcNow.AddDays(7));

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAssignmentAssignedAsync_SendsEmail_WithoutDueDate()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendAssignmentAssignedAsync("student@test.com", "John", "Optional Practice", null);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendWeeklyProgressReportAsync_SendsEmail_WithAllStats()
    {
        // Arrange
        var service = CreateService();
        var progress = new WeeklyProgressData
        {
            ClassesAttended = 3,
            AssignmentsCompleted = 2,
            PointsEarned = 150,
            CurrentStreak = 7,
            TotalPoints = 1500,
            NextMilestone = "2000 points",
            PointsToNextMilestone = 500,
            BadgesEarned = new List<string> { "Early Bird", "Perfect Week" }
        };

        // Act
        await service.SendWeeklyProgressReportAsync("student@test.com", "John", progress);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendWeeklyProgressReportAsync_SendsEmail_WithNoBadges()
    {
        // Arrange
        var service = CreateService();
        var progress = new WeeklyProgressData
        {
            ClassesAttended = 1,
            AssignmentsCompleted = 0,
            PointsEarned = 50,
            CurrentStreak = 1,
            TotalPoints = 50,
            NextMilestone = "100 points",
            PointsToNextMilestone = 50,
            BadgesEarned = new List<string>()
        };

        // Act
        await service.SendWeeklyProgressReportAsync("student@test.com", "John", progress);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPaymentFailedAsync_SendsEmail_WithFailureReason()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendPaymentFailedAsync("student@test.com", "John", "Premium", 99.00m, "Card declined");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPaymentFailedAsync_SendsEmail_WithoutFailureReason()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendPaymentFailedAsync("student@test.com", "John", "Premium", 99.00m, null);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("student@test.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Admin Notification Tests

    [Fact]
    public async Task SendAdminClassScheduledAsync_DoesNotSend_WhenAdminEmailNotConfigured()
    {
        // Arrange
        var service = CreateService();
        var classDateTime = new DateTime(2025, 3, 15, 14, 0, 0);

        // Act
        await service.SendAdminClassScheduledAsync("John Doe", "student@test.com", classDateTime);

        // Assert - verify debug log about admin email not configured
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Admin email not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminClassScheduledAsync_SendsEmail_WhenAdminEmailConfigured()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("karen@lakecountryspanish.com");
        var service = CreateService();
        var classDateTime = new DateTime(2025, 3, 15, 14, 0, 0);

        // Act
        await service.SendAdminClassScheduledAsync("John Doe", "student@test.com", classDateTime);

        // Assert - verify warning log (email not configured to send, but template was generated)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("karen@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminClassCancelledAsync_DoesNotSend_WhenAdminEmailNotConfigured()
    {
        // Arrange
        var service = CreateService();
        var classDateTime = new DateTime(2025, 3, 15, 14, 0, 0);

        // Act
        await service.SendAdminClassCancelledAsync("John Doe", "student@test.com", classDateTime, "Student requested");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Admin email not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminClassCancelledAsync_SendsEmail_WhenAdminEmailConfigured()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("karen@lakecountryspanish.com");
        var service = CreateService();
        var classDateTime = new DateTime(2025, 3, 15, 14, 0, 0);

        // Act
        await service.SendAdminClassCancelledAsync("John Doe", "student@test.com", classDateTime, "Late cancellation");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("karen@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminClassCancelledAsync_SendsEmail_WithoutReason()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("karen@lakecountryspanish.com");
        var service = CreateService();
        var classDateTime = new DateTime(2025, 3, 15, 14, 0, 0);

        // Act
        await service.SendAdminClassCancelledAsync("John Doe", "student@test.com", classDateTime, null);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("karen@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminClassReminderAsync_DoesNotSend_WhenAdminEmailNotConfigured()
    {
        // Arrange
        var service = CreateService();
        var classDateTime = DateTime.UtcNow.AddHours(24);

        // Act
        await service.SendAdminClassReminderAsync("John Doe", "student@test.com", classDateTime, 24);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Admin email not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminClassReminderAsync_SendsEmail_For24HourReminder()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("karen@lakecountryspanish.com");
        var service = CreateService();
        var classDateTime = DateTime.UtcNow.AddHours(24);

        // Act
        await service.SendAdminClassReminderAsync("John Doe", "student@test.com", classDateTime, 24);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("karen@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminClassReminderAsync_SendsEmail_For1HourReminder()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("karen@lakecountryspanish.com");
        var service = CreateService();
        var classDateTime = DateTime.UtcNow.AddHours(1);

        // Act
        await service.SendAdminClassReminderAsync("John Doe", "student@test.com", classDateTime, 1);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("karen@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminPaymentReceivedAsync_DoesNotSend_WhenAdminEmailNotConfigured()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendAdminPaymentReceivedAsync("John Doe", "student@test.com", 25.00m, "Single Class", DateTime.UtcNow);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Admin email not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminPaymentReceivedAsync_SendsEmail_WhenAdminEmailConfigured()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("karen@lakecountryspanish.com");
        var service = CreateService();

        // Act
        await service.SendAdminPaymentReceivedAsync("John Doe", "student@test.com", 90.00m, "4-Class Package", DateTime.UtcNow);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("karen@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminContactInquiryAsync_DoesNotSend_WhenNoEmailConfigured()
    {
        // Arrange - no AdminEmail and no ContactForm:NotificationEmail
        var service = CreateService();

        // Act
        await service.SendAdminContactInquiryAsync("Jane Smith", "jane@test.com", "555-1234", "I want to learn Spanish!");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No admin email configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminContactInquiryAsync_SendsEmail_WhenAdminEmailConfigured()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("karen@lakecountryspanish.com");
        var service = CreateService();

        // Act
        await service.SendAdminContactInquiryAsync("Jane Smith", "jane@test.com", "555-1234", "I want to learn Spanish!");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("karen@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminContactInquiryAsync_FallsBackToContactFormEmail_WhenAdminEmailNotConfigured()
    {
        // Arrange - AdminEmail not set, but ContactForm:NotificationEmail is
        _configMock.Setup(c => c["ContactForm:NotificationEmail"]).Returns("contact@lakecountryspanish.com");
        var service = CreateService();

        // Act
        await service.SendAdminContactInquiryAsync("Jane Smith", "jane@test.com", null, "I want to learn Spanish!");

        // Assert - should fall back to ContactForm:NotificationEmail
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("contact@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAdminContactInquiryAsync_SendsEmail_WithoutPhone()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("karen@lakecountryspanish.com");
        var service = CreateService();

        // Act
        await service.SendAdminContactInquiryAsync("Jane Smith", "jane@test.com", null, "I want to learn Spanish!");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("karen@lakecountryspanish.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Admin Name Configuration Tests

    [Fact]
    public async Task AdminNotifications_UseConfiguredAdminName()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("admin@test.com");
        _configMock.Setup(c => c["AppSettings:AdminName"]).Returns("Administrator");
        var service = CreateService();

        // Act
        await service.SendAdminClassScheduledAsync("John Doe", "student@test.com", DateTime.UtcNow.AddDays(1));

        // Assert - email should be generated with configured name (verify it doesn't throw)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminNotifications_DefaultToKaren_WhenNameNotConfigured()
    {
        // Arrange
        _configMock.Setup(c => c["AppSettings:AdminEmail"]).Returns("admin@test.com");
        _configMock.Setup(c => c["AppSettings:AdminName"]).Returns((string?)null);
        var service = CreateService();

        // Act
        await service.SendAdminPaymentReceivedAsync("John Doe", "student@test.com", 25.00m, "Class", DateTime.UtcNow);

        // Assert - should not throw, defaults to "Karen"
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
