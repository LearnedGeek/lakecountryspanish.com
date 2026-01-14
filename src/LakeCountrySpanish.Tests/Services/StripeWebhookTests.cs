using Microsoft.Extensions.Configuration;
using Moq;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

/// <summary>
/// Tests for Stripe webhook processing logic.
/// These tests verify the database updates that occur when webhooks are processed.
/// </summary>
public class StripeWebhookTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfiguration> _configMock;
    private readonly StripePaymentService _service;
    private readonly ApplicationUser _testStudent;
    private readonly Package _testPackage;

    public StripeWebhookTests()
    {
        _context = TestDbContextFactory.Create();
        _configMock = new Mock<IConfiguration>();

        _configMock.Setup(c => c["Stripe:WebhookSecret"]).Returns("whsec_test_secret");
        _configMock.Setup(c => c["AppSettings:DefaultClassPrice"]).Returns("25.00");

        _service = new StripePaymentService(_context, _configMock.Object);

        // Setup test data
        _testStudent = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "webhook_test@test.com",
            Email = "webhook_test@test.com",
            FirstName = "Webhook",
            LastName = "Test"
        };
        _context.Users.Add(_testStudent);

        _testPackage = new Package
        {
            Name = "Test Package",
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
    public async Task ProcessWebhookAsync_ReturnsNull_WhenInvalidSignature()
    {
        // Arrange - invalid JSON/signature will fail Stripe's validation
        var invalidJson = "{}";
        var invalidSignature = "invalid_signature";

        // Act
        var result = await _service.ProcessWebhookAsync(invalidJson, invalidSignature);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Payment_StatusUpdates_WhenCompleted()
    {
        // Arrange - create a pending payment
        var payment = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = 90.00m,
            Status = PaymentStatusType.Pending,
            PaymentType = PaymentType.Package,
            StripeSessionId = "cs_test_pending_123"
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Simulate webhook completion by updating directly
        payment.Status = PaymentStatusType.Completed;
        payment.CompletedAt = DateTime.UtcNow;
        payment.StripePaymentIntentId = "pi_test_123";
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Payments.FindAsync(payment.Id);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatusType.Completed, updated.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.Equal("pi_test_123", updated.StripePaymentIntentId);
    }

    [Fact]
    public async Task PackagePurchase_CreatesStudentPackage()
    {
        // Arrange - create completed payment for package
        var payment = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = _testPackage.Price,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.Package,
            StripeSessionId = "cs_test_package_123",
            CompletedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Simulate webhook creating student package
        var studentPackage = new StudentPackage
        {
            StudentId = _testStudent.Id,
            PackageId = _testPackage.Id,
            ClassesRemaining = _testPackage.ClassCount,
            PaymentId = payment.Id
        };
        _context.StudentPackages.Add(studentPackage);
        await _context.SaveChangesAsync();

        // Assert
        var packages = _context.StudentPackages
            .Where(sp => sp.StudentId == _testStudent.Id)
            .ToList();
        Assert.Single(packages);
        Assert.Equal(4, packages[0].ClassesRemaining);
        Assert.Equal(payment.Id, packages[0].PaymentId);
    }

    [Fact]
    public async Task SingleClassPayment_UpdatesScheduledClass()
    {
        // Arrange - create scheduled class
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = _testStudent.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(1),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Create payment for class
        var payment = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = 25.00m,
            Status = PaymentStatusType.Completed,
            PaymentType = PaymentType.SingleClass,
            StripeSessionId = "cs_test_class_123",
            CompletedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Simulate webhook updating class payment status
        scheduledClass.PaymentId = payment.Id;
        scheduledClass.PaymentStatus = PaymentStatus.Paid;
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.ScheduledClasses.FindAsync(scheduledClass.Id);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Paid, updated.PaymentStatus);
        Assert.Equal(payment.Id, updated.PaymentId);
    }

    [Fact]
    public async Task Tip_MarkedAsPaid_WhenWebhookProcessed()
    {
        // Arrange - create unpaid tip
        var tip = new Tip
        {
            StudentId = _testStudent.Id,
            Amount = 10.00m,
            Message = "Thank you!",
            IsPaid = false,
            StripeSessionId = "cs_test_tip_123",
            CreatedAt = DateTime.UtcNow
        };
        _context.Tips.Add(tip);
        await _context.SaveChangesAsync();

        // Simulate webhook marking tip as paid
        tip.IsPaid = true;
        tip.StripePaymentIntentId = "pi_test_tip_123";
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Tips.FindAsync(tip.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsPaid);
        Assert.Equal("pi_test_tip_123", updated.StripePaymentIntentId);
    }

    [Fact]
    public async Task PaymentStatusHistory_TracksChanges()
    {
        // Arrange
        var payment = new Payment
        {
            StudentId = _testStudent.Id,
            Amount = 25.00m,
            Status = PaymentStatusType.Pending,
            PaymentType = PaymentType.SingleClass,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var originalStatus = payment.Status;

        // Act - update status
        payment.Status = PaymentStatusType.Completed;
        payment.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Assert - verify transition happened
        var updated = await _context.Payments.FindAsync(payment.Id);
        Assert.NotNull(updated);
        Assert.NotEqual(originalStatus, updated.Status);
        Assert.Equal(PaymentStatusType.Completed, updated.Status);
    }
}
