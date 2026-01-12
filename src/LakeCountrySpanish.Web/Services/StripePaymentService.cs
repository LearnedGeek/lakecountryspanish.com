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

    public StripePaymentService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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

    public async Task<Payment?> ProcessWebhookAsync(string json, string stripeSignature)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                if (session == null) return null;

                var paymentIdStr = session.Metadata["paymentId"];
                if (!int.TryParse(paymentIdStr, out int paymentId))
                    return null;

                var payment = await _context.Payments
                    .Include(p => p.Student)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null) return null;

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
                        var studentPackage = new StudentPackage
                        {
                            StudentId = payment.StudentId,
                            PackageId = packageId,
                            ClassesRemaining = package.ClassCount,
                            PaymentId = payment.Id
                        };
                        _context.StudentPackages.Add(studentPackage);
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
                    }
                }

                await _context.SaveChangesAsync();
                return payment;
            }

            return null;
        }
        catch (StripeException)
        {
            return null;
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
        return defaultPrice > 0 ? defaultPrice : 25.00m;
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
        catch (StripeException)
        {
            // Remove the tip record if Stripe session creation fails
            _context.Tips.Remove(tip);
            await _context.SaveChangesAsync();
            return null;
        }
    }

    public async Task<bool> ProcessTipWebhookAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return false;

        var tip = await _context.Tips.FirstOrDefaultAsync(t => t.StripeSessionId == sessionId);
        if (tip == null)
            return false;

        // Check with Stripe if the session is paid
        try
        {
            var service = new SessionService();
            var session = await service.GetAsync(sessionId);

            if (session.PaymentStatus == "paid")
            {
                tip.IsPaid = true;
                tip.StripePaymentIntentId = session.PaymentIntentId;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (StripeException)
        {
            return false;
        }
    }
}
