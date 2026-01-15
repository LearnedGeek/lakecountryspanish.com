using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Stripe;
using Stripe.Checkout;

namespace LakeCountrySpanish.Web.Services;

public class StripePaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<StripePaymentService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> CreateCheckoutSessionAsync(string studentId, int? packageId, int? classId, decimal amount, string successUrl, string cancelUrl)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
            throw new ArgumentException("Student not found");

        string description;
        PaymentType paymentType;

        if (packageId.HasValue)
        {
            var package = await _context.Packages.FindAsync(packageId.Value);
            if (package == null)
                throw new ArgumentException("Package not found");
            description = $"Spanish Lessons Package: {package.Name}";
            paymentType = PaymentType.Package;
        }
        else if (classId.HasValue)
        {
            description = "Spanish Lesson - Single Class";
            paymentType = PaymentType.SingleClass;
        }
        else
        {
            description = "Spanish Lessons Payment";
            paymentType = PaymentType.SingleClass;
        }

        // Create payment record
        var payment = new Payment
        {
            StudentId = studentId,
            Amount = amount,
            Status = PaymentStatusType.Pending,
            PaymentType = paymentType,
            Description = description
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Create Stripe checkout session
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(amount * 100), // Stripe uses cents
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = description,
                        },
                    },
                    Quantity = 1,
                },
            },
            Mode = "payment",
            SuccessUrl = $"{successUrl}?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = cancelUrl,
            CustomerEmail = student.Email,
            Metadata = new Dictionary<string, string>
            {
                { "paymentId", payment.Id.ToString() },
                { "studentId", studentId },
                { "packageId", packageId?.ToString() ?? "" },
                { "classId", classId?.ToString() ?? "" }
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        // Update payment with session ID
        payment.StripeSessionId = session.Id;
        await _context.SaveChangesAsync();

        return session.Url!;
    }

    public async Task<WebhookProcessingResult> ProcessWebhookAsync(string json, string stripeSignature)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature validation failed");
            return WebhookProcessingResult.SignatureInvalid();
        }

        _logger.LogInformation("Processing Stripe webhook event: {EventType}, ID: {EventId}",
            stripeEvent.Type, stripeEvent.Id);

        try
        {
            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                if (session == null)
                {
                    _logger.LogWarning("Webhook event {EventId} had null session object", stripeEvent.Id);
                    return WebhookProcessingResult.Failed("Session object was null");
                }

                // Check for tip payment type
                if (session.Metadata.TryGetValue("type", out var paymentType) && paymentType == "tip")
                {
                    _logger.LogInformation("Webhook is for tip payment, skipping standard processing");
                    return WebhookProcessingResult.Skipped("Tip payment handled separately");
                }

                // Safely get paymentId from metadata - CLI test events won't have it
                if (!session.Metadata.TryGetValue("paymentId", out var paymentIdStr) ||
                    !int.TryParse(paymentIdStr, out int paymentId))
                {
                    _logger.LogInformation("Webhook event {EventId} missing paymentId metadata (likely CLI test)", stripeEvent.Id);
                    return WebhookProcessingResult.Skipped("No paymentId in metadata");
                }

                var payment = await _context.Payments
                    .Include(p => p.Student)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null)
                {
                    _logger.LogWarning("Payment {PaymentId} not found for webhook event {EventId}",
                        paymentId, stripeEvent.Id);
                    return WebhookProcessingResult.Failed($"Payment {paymentId} not found");
                }

                // IDEMPOTENCY CHECK: If payment is already completed with this PaymentIntentId, skip
                if (payment.Status == PaymentStatusType.Completed)
                {
                    if (payment.StripePaymentIntentId == session.PaymentIntentId)
                    {
                        _logger.LogInformation("Payment {PaymentId} already completed with PaymentIntent {PaymentIntentId}, skipping duplicate",
                            paymentId, session.PaymentIntentId);
                        return WebhookProcessingResult.Duplicate();
                    }
                    else
                    {
                        _logger.LogWarning("Payment {PaymentId} already completed but with different PaymentIntent. Existing: {ExistingPI}, New: {NewPI}",
                            paymentId, payment.StripePaymentIntentId, session.PaymentIntentId);
                        return WebhookProcessingResult.Duplicate();
                    }
                }

                // Additional idempotency: Check if this PaymentIntentId was already processed for ANY payment
                if (!string.IsNullOrEmpty(session.PaymentIntentId))
                {
                    var existingWithSamePI = await _context.Payments
                        .AnyAsync(p => p.StripePaymentIntentId == session.PaymentIntentId &&
                                       p.Status == PaymentStatusType.Completed);
                    if (existingWithSamePI)
                    {
                        _logger.LogWarning("PaymentIntent {PaymentIntentId} already processed for another payment",
                            session.PaymentIntentId);
                        return WebhookProcessingResult.Duplicate();
                    }
                }

                payment.Status = PaymentStatusType.Completed;
                payment.StripePaymentIntentId = session.PaymentIntentId;
                payment.CompletedAt = DateTime.UtcNow;

                // Handle package purchase
                if (session.Metadata.TryGetValue("packageId", out var packageIdStr) &&
                    !string.IsNullOrEmpty(packageIdStr) &&
                    int.TryParse(packageIdStr, out int packageId))
                {
                    var package = await _context.Packages.FindAsync(packageId);
                    if (package != null)
                    {
                        // Check if StudentPackage already exists for this payment (idempotency)
                        var existingStudentPackage = await _context.StudentPackages
                            .AnyAsync(sp => sp.PaymentId == payment.Id);

                        if (!existingStudentPackage)
                        {
                            var studentPackage = new StudentPackage
                            {
                                StudentId = payment.StudentId,
                                PackageId = packageId,
                                ClassesRemaining = package.ClassCount,
                                PaymentId = payment.Id
                            };
                            _context.StudentPackages.Add(studentPackage);
                            _logger.LogInformation("Created StudentPackage for payment {PaymentId}, package {PackageId}, {ClassCount} classes",
                                payment.Id, packageId, package.ClassCount);
                        }
                        else
                        {
                            _logger.LogInformation("StudentPackage already exists for payment {PaymentId}", payment.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Package {PackageId} not found for payment {PaymentId}", packageId, payment.Id);
                    }
                }

                // Handle single class payment
                if (session.Metadata.TryGetValue("classId", out var classIdStr) &&
                    !string.IsNullOrEmpty(classIdStr) &&
                    int.TryParse(classIdStr, out int classId))
                {
                    var scheduledClass = await _context.ScheduledClasses.FindAsync(classId);
                    if (scheduledClass != null)
                    {
                        scheduledClass.PaymentId = payment.Id;
                        scheduledClass.PaymentStatus = PaymentStatus.Paid;
                        _logger.LogInformation("Marked class {ClassId} as paid for payment {PaymentId}", classId, payment.Id);
                    }
                    else
                    {
                        _logger.LogWarning("ScheduledClass {ClassId} not found for payment {PaymentId}", classId, payment.Id);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully processed payment {PaymentId} for amount {Amount}",
                    payment.Id, payment.Amount);
                return WebhookProcessingResult.Succeeded(payment);
            }

            // Handle refund event
            if (stripeEvent.Type == "charge.refunded")
            {
                var charge = stripeEvent.Data.Object as Charge;
                if (charge != null && !string.IsNullOrEmpty(charge.PaymentIntentId))
                {
                    var payment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.StripePaymentIntentId == charge.PaymentIntentId);

                    if (payment != null)
                    {
                        payment.Status = PaymentStatusType.Refunded;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Payment {PaymentId} marked as refunded", payment.Id);
                        return WebhookProcessingResult.Succeeded(payment);
                    }
                }
                _logger.LogInformation("Refund event received but no matching payment found");
            }

            _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
            return WebhookProcessingResult.Skipped($"Unhandled event type: {stripeEvent.Type}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook event {EventId} of type {EventType}",
                stripeEvent.Id, stripeEvent.Type);
            return WebhookProcessingResult.Failed(ex.Message);
        }
    }

    public async Task<Payment?> GetPaymentBySessionIdAsync(string sessionId)
    {
        return await _context.Payments
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.StripeSessionId == sessionId);
    }

    public async Task<IEnumerable<Payment>> GetStudentPaymentsAsync(string studentId)
    {
        return await _context.Payments
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Payment>> GetAllPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null, string? studentId = null)
    {
        var query = _context.Payments
            .Include(p => p.Student)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(p => p.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.CreatedAt <= endDate.Value);

        if (!string.IsNullOrEmpty(studentId))
            query = query.Where(p => p.StudentId == studentId);

        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<decimal> GetStudentBalanceAsync(string studentId)
    {
        var unpaidClasses = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Where(sc => sc.StudentId == studentId &&
                        sc.PaymentStatus == PaymentStatus.Unpaid &&
                        sc.Status != ClassStatus.Cancelled)
            .CountAsync();

        var classPrice = await GetClassPriceForStudentAsync(studentId);
        return unpaidClasses * classPrice;
    }

    public async Task<decimal> GetClassPriceForStudentAsync(string studentId)
    {
        var student = await _context.Users.FindAsync(studentId);
        if (student?.CustomHourlyRate.HasValue == true)
            return student.CustomHourlyRate.Value;

        var defaultPrice = _configuration.GetValue<decimal>("AppSettings:DefaultClassPrice");
        if (defaultPrice > 0)
            return defaultPrice;

        // CRITICAL: Configuration is missing - log warning and use emergency fallback
        _logger.LogWarning(
            "AppSettings:DefaultClassPrice is not configured or is 0. Using emergency fallback of $25.00. " +
            "This should be configured in appsettings.json for production!");
        return 25.00m;
    }

    public async Task<string?> CreateTipCheckoutSessionAsync(string studentId, decimal amount, string? message, int? classId)
    {
        if (amount < 1 || amount > 1000)
            return null;

        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
            return null;

        // Create tip record
        var tip = new Tip
        {
            StudentId = studentId,
            Amount = amount,
            Message = message?.Trim(),
            ScheduledClassId = classId,
            IsPaid = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tips.Add(tip);
        await _context.SaveChangesAsync();

        // Build URLs
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:7001";
        var successUrl = $"{baseUrl}/Student/TipSuccess?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl}/Student/TipCancel";

        // Create Stripe checkout session
        try
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(amount * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Tip for Karen",
                                Description = string.IsNullOrEmpty(message) ? "Thank you for your support!" : message
                            },
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                CustomerEmail = student.Email,
                Metadata = new Dictionary<string, string>
                {
                    { "tipId", tip.Id.ToString() },
                    { "studentId", studentId },
                    { "type", "tip" }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            // Update tip with session ID
            tip.StripeSessionId = session.Id;
            await _context.SaveChangesAsync();

            return session.Url;
        }
        catch (StripeException ex)
        {
            // Remove the tip record if Stripe session creation fails
            _logger.LogError(ex, "Failed to create Stripe checkout session for tip. StudentId: {StudentId}, Amount: {Amount}",
                studentId, amount);
            _context.Tips.Remove(tip);
            await _context.SaveChangesAsync();
            return null;
        }
    }

    public async Task<bool> ProcessTipWebhookAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            _logger.LogWarning("ProcessTipWebhookAsync called with empty sessionId");
            return false;
        }

        var tip = await _context.Tips.FirstOrDefaultAsync(t => t.StripeSessionId == sessionId);
        if (tip == null)
        {
            _logger.LogWarning("Tip not found for session {SessionId}", sessionId);
            return false;
        }

        // Check with Stripe if the session is paid
        try
        {
            var service = new SessionService();
            var session = await service.GetAsync(sessionId);

            if (session.PaymentStatus == "paid")
            {
                // Idempotency check: if already paid, don't process again
                if (tip.IsPaid)
                {
                    _logger.LogInformation("Tip {TipId} already marked as paid, skipping duplicate processing", tip.Id);
                    return true;
                }

                tip.IsPaid = true;
                tip.StripePaymentIntentId = session.PaymentIntentId;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Tip {TipId} marked as paid. Amount: {Amount}, StudentId: {StudentId}",
                    tip.Id, tip.Amount, tip.StudentId);
                return true;
            }

            _logger.LogInformation("Tip {TipId} session {SessionId} not yet paid. Status: {Status}",
                tip.Id, sessionId, session.PaymentStatus);
            return false;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error while processing tip webhook for session {SessionId}, tip {TipId}",
                sessionId, tip.Id);
            return false;
        }
    }
}
