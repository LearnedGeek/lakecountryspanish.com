using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Implementation of the class scheduling checkout flow.
/// </summary>
public class ClassSchedulingService : IClassSchedulingService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IScheduleService _scheduleService;
    private readonly IPaymentService _paymentService;
    private readonly ITicketService _ticketService;
    private readonly ISubscriptionService _subscriptionService;

    public ClassSchedulingService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IScheduleService scheduleService,
        IPaymentService paymentService,
        ITicketService ticketService,
        ISubscriptionService subscriptionService)
    {
        _context = context;
        _configuration = configuration;
        _scheduleService = scheduleService;
        _paymentService = paymentService;
        _ticketService = ticketService;
        _subscriptionService = subscriptionService;
    }

    public async Task<ScheduledClass?> AddClassToCheckoutAsync(string studentId, int timeSlotId, DateTime classDateTime)
    {
        // Verify the slot is available
        if (!await _scheduleService.IsTimeSlotAvailableAsync(timeSlotId, classDateTime))
        {
            return null;
        }

        // Check booking restrictions (must be > 1 hour from now)
        if (!_scheduleService.CanBookClass(classDateTime))
        {
            return null;
        }

        // Check if student already has this slot in pending checkout
        var existingPending = await _context.ScheduledClasses
            .AnyAsync(sc => sc.StudentId == studentId
                && sc.TimeSlotId == timeSlotId
                && sc.ClassDateTime == classDateTime
                && sc.IsPendingCheckout);

        if (existingPending)
        {
            return null;
        }

        // Create the pending class
        var scheduledClass = new ScheduledClass
        {
            StudentId = studentId,
            TimeSlotId = timeSlotId,
            ClassDateTime = classDateTime,
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPendingCheckout = true,
            CheckoutBatchId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Load the time slot for the response
        await _context.Entry(scheduledClass).Reference(sc => sc.TimeSlot).LoadAsync();

        return scheduledClass;
    }

    public async Task<bool> RemoveFromCheckoutAsync(int classId, string studentId)
    {
        var scheduledClass = await _context.ScheduledClasses
            .FirstOrDefaultAsync(sc => sc.Id == classId
                && sc.StudentId == studentId
                && sc.IsPendingCheckout);

        if (scheduledClass == null)
        {
            return false;
        }

        _context.ScheduledClasses.Remove(scheduledClass);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<ScheduledClass>> GetPendingCheckoutAsync(string studentId)
    {
        return await _context.ScheduledClasses
            .Include(sc => sc.TimeSlot)
            .Where(sc => sc.StudentId == studentId && sc.IsPendingCheckout)
            .OrderBy(sc => sc.ClassDateTime)
            .ToListAsync();
    }

    public async Task<int> ClearPendingCheckoutAsync(string studentId)
    {
        var pendingClasses = await _context.ScheduledClasses
            .Where(sc => sc.StudentId == studentId && sc.IsPendingCheckout)
            .ToListAsync();

        if (!pendingClasses.Any())
        {
            return 0;
        }

        _context.ScheduledClasses.RemoveRange(pendingClasses);
        await _context.SaveChangesAsync();

        return pendingClasses.Count;
    }

    public async Task<SchedulingCheckoutSummary> GetCheckoutSummaryAsync(string studentId, int ticketsToApply = 0)
    {
        var pendingClasses = await GetPendingCheckoutAsync(studentId);
        var classCount = pendingClasses.Count();
        var perClassPrice = await _paymentService.GetClassPriceForStudentAsync(studentId);

        // Get available tickets
        var availableTickets = await _ticketService.GetAvailableTicketCountAsync(studentId);
        var actualTicketsToApply = Math.Min(ticketsToApply, Math.Min(availableTickets, classCount));

        var subtotal = classCount * perClassPrice;
        var ticketDiscount = actualTicketsToApply * perClassPrice;
        var totalDue = subtotal - ticketDiscount;

        return new SchedulingCheckoutSummary
        {
            ClassCount = classCount,
            PerClassPrice = perClassPrice,
            Subtotal = subtotal,
            TicketsApplied = actualTicketsToApply,
            TicketDiscount = ticketDiscount,
            TotalDue = Math.Max(0, totalDue)
        };
    }

    public async Task<string?> CreateCheckoutSessionAsync(string studentId, int ticketsToApply, string successUrl, string cancelUrl)
    {
        var pendingClasses = (await GetPendingCheckoutAsync(studentId)).ToList();
        if (!pendingClasses.Any())
        {
            return null;
        }

        var summary = await GetCheckoutSummaryAsync(studentId, ticketsToApply);

        // If total is $0 (all covered by tickets), process immediately
        if (summary.TotalDue <= 0)
        {
            // Apply tickets directly and confirm classes
            var confirmResult = await ConfirmClassesWithTicketsAsync(studentId, pendingClasses, summary.TicketsApplied);
            if (confirmResult)
            {
                // Return success URL directly since no payment needed
                return successUrl + "?free=true";
            }
            return null;
        }

        // Create a checkout batch ID for this group
        var batchId = Guid.NewGuid();
        foreach (var cls in pendingClasses)
        {
            cls.CheckoutBatchId = batchId;
        }
        await _context.SaveChangesAsync();

        // Build line items description
        var lineItemDescription = $"{summary.ClassCount} Spanish class(es)";
        if (summary.TicketsApplied > 0)
        {
            lineItemDescription += $" (with {summary.TicketsApplied} free class reward(s) applied)";
        }

        // Create Stripe checkout session
        var checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(
            studentId,
            packageId: null,
            classId: null, // We'll use metadata to track the batch
            amount: summary.TotalDue,
            successUrl: successUrl + $"?batchId={batchId}&tickets={summary.TicketsApplied}",
            cancelUrl: cancelUrl);

        return checkoutUrl;
    }

    public async Task<bool> CompleteCheckoutAsync(string stripeSessionId)
    {
        // Find the payment by session ID
        var payment = await _paymentService.GetPaymentBySessionIdAsync(stripeSessionId);
        if (payment == null)
        {
            return false;
        }

        // Find pending classes for this student (they should have a batch ID)
        var pendingClasses = await _context.ScheduledClasses
            .Where(sc => sc.StudentId == payment.StudentId && sc.IsPendingCheckout)
            .ToListAsync();

        if (!pendingClasses.Any())
        {
            return false;
        }

        // Confirm all pending classes
        foreach (var cls in pendingClasses)
        {
            cls.IsPendingCheckout = false;
            cls.PaymentId = payment.Id;
            cls.PaymentStatus = PaymentStatus.Paid;
        }

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<ScheduledClass?> ScheduleWithSubscriptionAsync(string studentId, int timeSlotId, DateTime classDateTime)
    {
        // Get active subscription
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(studentId);
        if (subscription == null || subscription.ClassesRemainingThisMonth <= 0)
        {
            return null;
        }

        // Verify the slot is available
        if (!await _scheduleService.IsTimeSlotAvailableAsync(timeSlotId, classDateTime))
        {
            return null;
        }

        // Check booking restrictions
        if (!_scheduleService.CanBookClass(classDateTime))
        {
            return null;
        }

        // Create confirmed class
        var scheduledClass = new ScheduledClass
        {
            StudentId = studentId,
            TimeSlotId = timeSlotId,
            ClassDateTime = classDateTime,
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PartOfSubscription,
            SubscriptionId = subscription.Id,
            IsPendingCheckout = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ScheduledClasses.Add(scheduledClass);

        // Increment subscription usage (ClassesRemainingThisMonth is calculated from Tier.ClassesPerMonth - ClassesUsedThisMonth)
        subscription.ClassesUsedThisMonth++;

        await _context.SaveChangesAsync();

        // Load time slot for response
        await _context.Entry(scheduledClass).Reference(sc => sc.TimeSlot).LoadAsync();

        return scheduledClass;
    }

    public async Task<int> CleanupStalePendingClassesAsync(int olderThanMinutes = 30)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-olderThanMinutes);

        var staleClasses = await _context.ScheduledClasses
            .Where(sc => sc.IsPendingCheckout && sc.CreatedAt < cutoff)
            .ToListAsync();

        if (!staleClasses.Any())
        {
            return 0;
        }

        _context.ScheduledClasses.RemoveRange(staleClasses);
        await _context.SaveChangesAsync();

        return staleClasses.Count;
    }

    /// <summary>
    /// Helper method to confirm classes paid with tickets (no Stripe payment needed).
    /// </summary>
    private async Task<bool> ConfirmClassesWithTicketsAsync(string studentId, List<ScheduledClass> pendingClasses, int ticketsToApply)
    {
        if (ticketsToApply < pendingClasses.Count)
        {
            return false; // Not enough tickets to cover all classes
        }

        // Use tickets for each class
        foreach (var cls in pendingClasses)
        {
            var ticket = await _ticketService.UseTicketForClassAsync(studentId, cls.Id);
            if (ticket == null)
            {
                // Rollback any previous ticket uses and fail
                return false;
            }

            cls.IsPendingCheckout = false;
            cls.TicketId = ticket.Id;
            cls.PaymentStatus = PaymentStatus.PaidWithTicket;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}
