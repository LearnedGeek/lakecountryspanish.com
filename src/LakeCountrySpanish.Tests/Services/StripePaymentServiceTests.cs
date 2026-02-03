using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

/// <summary>
/// Integration tests for Stripe payment service.
/// These tests verify the payment flow logic without hitting real Stripe APIs.
/// For full E2E testing with Stripe, use stripe listen --forward-to localhost
/// </summary>
public class StripePaymentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<StripePaymentService>> _loggerMock;
    private readonly StripePaymentService _service;
    private readonly ApplicationUser _testStudent;
    private readonly Package _testPackage;

    public StripePaymentServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _configMock = new Mock<IConfiguration>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<StripePaymentService>>();

        // Setup configuration
        _configMock.Setup(c => c["Stripe:WebhookSecret"]).Returns("whsec_test_secret");
        _configMock.Setup(c => c["AppSettings:DefaultClassPrice"]).Returns("25.00");
        _configMock.Setup(c => c["AppSettings:BaseUrl"]).Returns("https://localhost:5001");

        var configSection = new Mock<IConfigurationSection>();
        configSection.Setup(s => s.Value).Returns("25.00");
        _configMock.Setup(c => c.GetSection("AppSettings:DefaultClassPrice")).Returns(configSection.Object);

        _service = new StripePaymentService(_context, _configMock.Object, _emailServiceMock.Object, _loggerMock.Object);

        // Setup test data
        _testStudent = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "teststudent@test.com",
            Email = "teststudent@test.com",
            FirstName = "Test",
            LastName = "Student"
        };
        _context.Users.Add(_testStudent);

        _testPackage = new Package
        {
            Name = "4-Class Package",
            ClassCount = 4,
            Price = 90.00m,
            IsActive = true
        };
        _context.Packages.Add(_testPackage);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetClassPriceForStudentAsync_ReturnsDefaultPrice_WhenNoCustomRate()
    {
        // Arrange - student has no custom rate

        // Act
        var price = await _service.GetClassPriceForStudentAsync(_testStudent.Id);

        // Assert
        Assert.Equal(25.00m, price);
    }

    [Fact]
    public async Task GetClassPriceForStudentAsync_ReturnsCustomRate_WhenSet()
    {
        // Arrange
        _testStudent.CustomHourlyRate = 35.00m;
        await _context.SaveChangesAsync();

        // Act
        var price = await _service.GetClassPriceForStudentAsync(_testStudent.Id);

        // Assert
        Assert.Equal(35.00m, price);
    }

    [Fact]
    public async Task GetStudentBalanceAsync_ReturnsZero_WhenNoUnpaidClasses()
    {
        // Act
        var balance = await _service.GetStudentBalanceAsync(_testStudent.Id);

        // Assert
        Assert.Equal(0m, balance);
    }

    [Fact]
    public async Task GetStudentBalanceAsync_CalculatesCorrectBalance_WithUnpaidClasses()
    {
        // Arrange - create unpaid scheduled classes
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 3; i++)
        {
            _context.ScheduledClasses.Add(new ScheduledClass
            {
                StudentId = _testStudent.Id,
                TimeSlotId = timeSlot.Id,
                ClassDateTime = DateTime.Today.AddDays(i + 1),
                Status = ClassStatus.Scheduled,
                PaymentStatus = PaymentStatus.Unpaid
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var balance = await _service.GetStudentBalanceAsync(_testStudent.Id);

        // Assert - 3 classes * $25 = $75
        Assert.Equal(75.00m, balance);
    }

    [Fact]
    public async Task GetStudentBalanceAsync_ExcludesCancelledClasses()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        // Add one scheduled and one cancelled class
        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = _testStudent.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(1),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid
        });
        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = _testStudent.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(2),
            Status = ClassStatus.Cancelled,
            PaymentStatus = PaymentStatus.Unpaid
        });
        await _context.SaveChangesAsync();

        // Act
        var balance = await _service.GetStudentBalanceAsync(_testStudent.Id);

        // Assert - only 1 class * $25 = $25 (cancelled excluded)
        Assert.Equal(25.00m, balance);
    }

    [Fact]
    public async Task GetStudentPaymentsAsync_ReturnsEmptyList_WhenNoPayments()
    {
        // Act
        var payments = await _service.GetStudentPaymentsAsync(_testStudent.Id);

        // Assert
        Assert.Empty(payments);
    }

    [Fact]
    public async Task GetStudentPaymentsAsync_ReturnsPayments_OrderedByDateDescending()
    {
        // Arrange
        var payment1 = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = 25.00m,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.SingleClass,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var payment2 = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = 90.00m,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.Package,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var payment3 = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = 25.00m,
            Status = PaymentStatusType.Pending,
            PaymentType = PaymentType.SingleClass,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.AddRange(payment1, payment2, payment3);
        await _context.SaveChangesAsync();

        // Act
        var payments = (await _service.GetStudentPaymentsAsync(_testStudent.Id)).ToList();

        // Assert
        Assert.Equal(3, payments.Count);
        Assert.Equal(payment3.Id, payments[0].Id); // Most recent first
        Assert.Equal(payment2.Id, payments[1].Id);
        Assert.Equal(payment1.Id, payments[2].Id);
    }

    [Fact]
    public async Task GetPaymentBySessionIdAsync_ReturnsNull_WhenNotFound()
    {
        // Act
        var payment = await _service.GetPaymentBySessionIdAsync("nonexistent_session");

        // Assert
        Assert.Null(payment);
    }

    [Fact]
    public async Task GetPaymentBySessionIdAsync_ReturnsPayment_WhenFound()
    {
        // Arrange
        var payment = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = 25.00m,
            Status = PaymentStatusType.Pending,
            PaymentType = PaymentType.SingleClass,
            StripeSessionId = "cs_test_session_123"
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPaymentBySessionIdAsync("cs_test_session_123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(payment.Id, result.Id);
        Assert.Equal(25.00m, result.Amount);
    }

    [Fact]
    public async Task GetAllPaymentsAsync_FiltersCorrectly()
    {
        // Arrange
        var student2 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "student2@test.com",
            Email = "student2@test.com",
            FirstName = "Test2",
            LastName = "Student2"
        };
        _context.Users.Add(student2);

        var payment1 = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = 25.00m,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.SingleClass,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        var payment2 = new Payment
        {
            StudentId = student2.Id,
            Amount = 90.00m,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.Package,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        _context.Payments.AddRange(payment1, payment2);
        await _context.SaveChangesAsync();

        // Act - filter by student
        var studentPayments = (await _service.GetAllPaymentsAsync(studentId: _testStudent.Id)).ToList();

        // Assert
        Assert.Single(studentPayments);
        Assert.Equal(_testStudent.Id, studentPayments[0].StudentId);

        // Act - filter by date range
        var recentPayments = (await _service.GetAllPaymentsAsync(
            startDate: DateTime.UtcNow.AddDays(-3),
            endDate: DateTime.UtcNow)).ToList();

        // Assert
        Assert.Single(recentPayments);
        Assert.Equal(student2.Id, recentPayments[0].StudentId);
    }
}
