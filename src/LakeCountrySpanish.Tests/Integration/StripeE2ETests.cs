using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;
using Stripe;
using Stripe.Checkout;

namespace LakeCountrySpanish.Tests.Integration;

/// <summary>
/// End-to-end integration tests that hit the real Stripe Test API.
///
/// PREREQUISITES:
/// 1. Either set environment variable STRIPE_TEST_SECRET_KEY, or
/// 2. Configure Stripe:SecretKey in appsettings.Development.json
/// 3. For webhook tests, run: stripe listen --forward-to http://localhost:5028/api/payment/webhook
///
/// These tests are skipped if no Stripe key is available.
/// Run with: dotnet test --filter "Category=StripeIntegration"
/// </summary>
[Trait("Category", "StripeIntegration")]
public class StripeE2ETests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<StripePaymentService>> _loggerMock;
    private readonly StripePaymentService _service;
    private readonly ApplicationUser _testStudent;
    private readonly Package _testPackage;
    private readonly string? _stripeSecretKey;

    public StripeE2ETests()
    {
        // First try environment variable (for CI/CD)
        _stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_TEST_SECRET_KEY");

        // Fall back to appsettings.Development.json (for local development)
        if (string.IsNullOrEmpty(_stripeSecretKey))
        {
            var devConfigPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..",
                "LakeCountrySpanish.Web",
                "appsettings.Development.json");

            if (System.IO.File.Exists(devConfigPath))
            {
                var devConfig = new ConfigurationBuilder()
                    .AddJsonFile(devConfigPath, optional: true)
                    .Build();

                _stripeSecretKey = devConfig["Stripe:SecretKey"];
            }
        }

        if (!string.IsNullOrEmpty(_stripeSecretKey))
        {
            StripeConfiguration.ApiKey = _stripeSecretKey;
        }

        _context = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<StripePaymentService>>();

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = _stripeSecretKey,
            ["Stripe:WebhookSecret"] = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "whsec_test",
            ["AppSettings:DefaultClassPrice"] = "25.00",
            ["AppSettings:BaseUrl"] = "https://localhost:5001"
        });
        _configuration = configBuilder.Build();

        _service = new StripePaymentService(_context, _configuration, _loggerMock.Object);

        // Setup test data
        _testStudent = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"e2e_test_{Guid.NewGuid():N}@test.com",
            Email = $"e2e_test_{Guid.NewGuid():N}@test.com",
            FirstName = "E2E",
            LastName = "Test"
        };
        _context.Users.Add(_testStudent);

        _testPackage = new Package
        {
            Name = "E2E Test Package",
            ClassCount = 4,
            Price = 1.00m, // Use $1 for test
            IsActive = true
        };
        _context.Packages.Add(_testPackage);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private void SkipIfNoStripeKey()
    {
        if (string.IsNullOrEmpty(_stripeSecretKey))
        {
            Assert.Fail("No Stripe key found. Set STRIPE_TEST_SECRET_KEY env var or configure Stripe:SecretKey in appsettings.Development.json.");
        }
    }

    [Fact]
    public async Task CreateCheckoutSession_CreatesValidStripeSession()
    {
        SkipIfNoStripeKey();

        // Arrange
        var successUrl = "https://localhost:5001/Payment/Success";
        var cancelUrl = "https://localhost:5001/Payment/Cancel";

        // Act
        var checkoutUrl = await _service.CreateCheckoutSessionAsync(
            _testStudent.Id,
            _testPackage.Id,
            null,
            _testPackage.Price,
            successUrl,
            cancelUrl);

        // Assert
        Assert.NotNull(checkoutUrl);
        Assert.Contains("checkout.stripe.com", checkoutUrl);

        // Verify payment record was created
        var payments = await _service.GetStudentPaymentsAsync(_testStudent.Id);
        var payment = payments.FirstOrDefault();
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatusType.Pending, payment.Status);
        Assert.NotNull(payment.StripeSessionId);
        Assert.StartsWith("cs_test_", payment.StripeSessionId);
    }

    [Fact]
    public async Task CreateCheckoutSession_ForSingleClass_CreatesValidSession()
    {
        SkipIfNoStripeKey();

        // Arrange
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Tuesday,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = _testStudent.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var successUrl = "https://localhost:5001/Payment/Success";
        var cancelUrl = "https://localhost:5001/Payment/Cancel";

        // Act
        var checkoutUrl = await _service.CreateCheckoutSessionAsync(
            _testStudent.Id,
            null,
            scheduledClass.Id,
            25.00m,
            successUrl,
            cancelUrl);

        // Assert
        Assert.NotNull(checkoutUrl);
        Assert.Contains("checkout.stripe.com", checkoutUrl);
    }

    [Fact]
    public async Task CreateTipCheckoutSession_CreatesValidSession()
    {
        SkipIfNoStripeKey();

        // Act
        var checkoutUrl = await _service.CreateTipCheckoutSessionAsync(
            _testStudent.Id,
            5.00m,
            "Thank you for the great lesson!",
            null);

        // Assert
        Assert.NotNull(checkoutUrl);
        Assert.Contains("checkout.stripe.com", checkoutUrl);

        // Verify tip record was created
        var tip = _context.Tips.FirstOrDefault(t => t.StudentId == _testStudent.Id);
        Assert.NotNull(tip);
        Assert.Equal(5.00m, tip.Amount);
        Assert.False(tip.IsPaid);
        Assert.NotNull(tip.StripeSessionId);
    }

    [Fact]
    public async Task CreateTipCheckoutSession_RejectsInvalidAmounts()
    {
        SkipIfNoStripeKey();

        // Act - too low
        var result1 = await _service.CreateTipCheckoutSessionAsync(_testStudent.Id, 0.50m, null, null);
        Assert.Null(result1);

        // Act - too high
        var result2 = await _service.CreateTipCheckoutSessionAsync(_testStudent.Id, 1500.00m, null, null);
        Assert.Null(result2);
    }

    [Fact]
    public async Task StripeSession_CanBeRetrieved()
    {
        SkipIfNoStripeKey();

        // Arrange - create a session first
        var successUrl = "https://localhost:5001/Payment/Success";
        var cancelUrl = "https://localhost:5001/Payment/Cancel";

        await _service.CreateCheckoutSessionAsync(
            _testStudent.Id,
            _testPackage.Id,
            null,
            _testPackage.Price,
            successUrl,
            cancelUrl);

        var payment = (await _service.GetStudentPaymentsAsync(_testStudent.Id)).FirstOrDefault();
        Assert.NotNull(payment?.StripeSessionId);

        // Act - retrieve session from Stripe
        var sessionService = new SessionService();
        var session = await sessionService.GetAsync(payment.StripeSessionId);

        // Assert
        Assert.NotNull(session);
        Assert.Equal("open", session.Status);
        Assert.Equal((long)(_testPackage.Price * 100), session.AmountTotal);
        Assert.Equal(payment.Id.ToString(), session.Metadata["paymentId"]);
    }

    [Fact]
    public async Task MultiplePayments_TrackCorrectly()
    {
        SkipIfNoStripeKey();

        // Arrange & Act - create multiple payments
        var successUrl = "https://localhost:5001/Payment/Success";
        var cancelUrl = "https://localhost:5001/Payment/Cancel";

        await _service.CreateCheckoutSessionAsync(_testStudent.Id, _testPackage.Id, null, _testPackage.Price, successUrl, cancelUrl);
        await _service.CreateCheckoutSessionAsync(_testStudent.Id, _testPackage.Id, null, _testPackage.Price, successUrl, cancelUrl);
        await _service.CreateCheckoutSessionAsync(_testStudent.Id, _testPackage.Id, null, _testPackage.Price, successUrl, cancelUrl);

        // Assert
        var payments = (await _service.GetStudentPaymentsAsync(_testStudent.Id)).ToList();
        Assert.Equal(3, payments.Count);
        Assert.All(payments, p => Assert.Equal(PaymentStatusType.Pending, p.Status));
        Assert.All(payments, p => Assert.StartsWith("cs_test_", p.StripeSessionId));

        // Each should have unique session ID
        var uniqueSessionIds = payments.Select(p => p.StripeSessionId).Distinct().Count();
        Assert.Equal(3, uniqueSessionIds);
    }
}
