using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = AppRoles.Student)]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IScheduleService _scheduleService;
    private readonly IClassSchedulingService _classSchedulingService;
    private readonly IPaymentService _paymentService;
    private readonly IGamificationService _gamificationService;
    private readonly IPlacementTestService _placementTestService;
    private readonly ITokenService _tokenService;
    private readonly ITicketService _ticketService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IEmailService _emailService;

    public StudentController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IScheduleService scheduleService,
        IClassSchedulingService classSchedulingService,
        IPaymentService paymentService,
        IGamificationService gamificationService,
        IPlacementTestService placementTestService,
        ITokenService tokenService,
        ITicketService ticketService,
        ISubscriptionService subscriptionService,
        IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _scheduleService = scheduleService;
        _classSchedulingService = classSchedulingService;
        _paymentService = paymentService;
        _gamificationService = gamificationService;
        _placementTestService = placementTestService;
        _tokenService = tokenService;
        _ticketService = ticketService;
        _subscriptionService = subscriptionService;
        _emailService = emailService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var classes = await _scheduleService.GetStudentClassesAsync(user.Id);
        var now = DateTime.UtcNow;

        var availablePackageClasses = await _context.StudentPackages
            .Where(sp => sp.StudentId == user.Id && sp.ClassesRemaining > 0)
            .SumAsync(sp => sp.ClassesRemaining);

        var documents = await _context.Documents
            .Where(d => d.IsGlobal || d.StudentDocuments.Any(sd => sd.StudentId == user.Id))
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        // Get completed classes from the last 30 days that don't have feedback yet
        var thirtyDaysAgo = now.AddDays(-30);
        var classesNeedingFeedback = await _context.ScheduledClasses
            .Where(c => c.StudentId == user.Id
                && c.Status == ClassStatus.Completed
                && c.ClassDateTime >= thirtyDaysAgo
                && c.ClassDateTime < now
                && !_context.ClassFeedbacks.Any(f => f.ScheduledClassId == c.Id && f.StudentId == user.Id))
            .OrderByDescending(c => c.ClassDateTime)
            .Take(5)
            .Select(c => new CompletedClassForFeedbackViewModel
            {
                ClassId = c.Id,
                ClassDateTime = c.ClassDateTime,
                TeacherNotes = c.TeacherNotes,
                HasFeedback = false
            })
            .ToListAsync();

        // Check if student has taken placement test
        var hasTakenPlacementTest = await _placementTestService.HasCompletedTestAsync(user.Id);

        // Get ticket and token balances
        var availableTickets = await _ticketService.GetAvailableTicketCountAsync(user.Id);
        var totalTokens = await _tokenService.GetTotalTokenBalanceAsync(user.Id);

        // Get gamification progress (streak, badges, etc.)
        var gamificationProgress = await _gamificationService.GetProgressAsync(user.Id);
        var streak = await _gamificationService.GetStreakAsync(user.Id);
        var unviewedBadges = await _gamificationService.GetUnviewedBadgesAsync(user.Id);
        var recentBadges = gamificationProgress.RecentBadges.Take(5);

        // Get activity stats
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var classesThisMonth = await _context.ScheduledClasses
            .CountAsync(c => c.StudentId == user.Id && c.Status == ClassStatus.Completed && c.ClassDateTime >= startOfMonth);
        var totalClassesCompleted = await _context.ScheduledClasses
            .CountAsync(c => c.StudentId == user.Id && c.Status == ClassStatus.Completed);
        var assignmentsThisWeek = await _context.StudentAssignments
            .CountAsync(a => a.StudentId == user.Id && a.Status == StudentAssignmentStatus.Completed && a.CompletedAt >= startOfWeek);

        // Get in-progress assignments
        var inProgressAssignments = await _context.StudentAssignments
            .Include(a => a.Assignment)
            .Where(a => a.StudentId == user.Id && a.Status == StudentAssignmentStatus.InProgress)
            .OrderBy(a => a.DueDate)
            .Take(3)
            .Select(a => new InProgressAssignmentViewModel
            {
                StudentAssignmentId = a.Id,
                Title = a.Assignment.Title,
                CefrLevel = a.Assignment.CefrLevel,
                TotalPoints = a.Assignment.TotalPoints,
                DueDate = a.DueDate
            })
            .ToListAsync();

        var upcomingClasses = classes.Where(c => c.ClassDateTime >= now && c.Status == ClassStatus.Scheduled).ToList();
        var nextClass = upcomingClasses.OrderBy(c => c.ClassDateTime).FirstOrDefault();

        var viewModel = new StudentDashboardViewModel
        {
            StudentName = user.FirstName,
            DefaultClassroomUrl = user.ClassroomUrl,
            UpcomingClasses = upcomingClasses,
            PastClasses = classes.Where(c => c.ClassDateTime < now || c.Status != ClassStatus.Scheduled).Take(10),
            Balance = await _paymentService.GetStudentBalanceAsync(user.Id),
#pragma warning disable CS0618 // Legacy field
            AvailablePackageClasses = availablePackageClasses,
#pragma warning restore CS0618
            AvailableTickets = availableTickets,
            TotalTokens = totalTokens,
            TotalPoints = user.TotalPoints,
            PointsToNextToken = 100 - (user.TotalPoints % 100),
            Documents = documents,
            ClassesNeedingFeedback = classesNeedingFeedback,
            CefrLevel = user.CefrLevel,
            HasTakenPlacementTest = hasTakenPlacementTest,

            // Gamification data
            CurrentStreak = streak.CurrentStreak,
            LongestStreak = streak.LongestStreak,
            ActivityToday = gamificationProgress.WasActiveToday,
            LastActivityDate = streak.LastActivityDate,
            TotalBadgesEarned = gamificationProgress.TotalBadgesEarned,
            RecentBadges = recentBadges.Select(sb => new StudentBadgeViewModel
            {
                BadgeId = sb.BadgeId,
                Name = sb.Badge.Name,
                Description = sb.Badge.Description,
                Emoji = sb.Badge.Emoji,
                IconUrl = sb.Badge.IconUrl,
                Category = sb.Badge.Category,
                EarnedAt = sb.EarnedAt,
                IsNew = !sb.IsViewed
            }).ToList(),
            NewBadges = unviewedBadges.Select(sb => new StudentBadgeViewModel
            {
                BadgeId = sb.BadgeId,
                Name = sb.Badge.Name,
                Description = sb.Badge.Description,
                Emoji = sb.Badge.Emoji,
                IconUrl = sb.Badge.IconUrl,
                Category = sb.Badge.Category,
                EarnedAt = sb.EarnedAt,
                IsNew = true
            }).ToList(),

            // Activity stats
            ClassesThisMonth = classesThisMonth,
            TotalClassesCompleted = totalClassesCompleted,
            AssignmentsCompletedThisWeek = assignmentsThisWeek,

            // Next class and in-progress
            NextClass = nextClass,
            InProgressAssignments = inProgressAssignments
        };

        return View(viewModel);
    }

    // Legacy PurchaseClasses - redirects to Token Store
    [HttpGet]
    public IActionResult PurchaseClasses()
    {
        return RedirectToAction(nameof(TokenStore));
    }

    // New unified scheduling page (replaces BookClass)
    [HttpGet]
    public async Task<IActionResult> MyClasses(DateTime? weekStart, string? tab)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Get available free class rewards (tickets)
        var availableTickets = await _ticketService.GetAvailableTicketCountAsync(user.Id);

        // Default to current week (Sunday start)
        var today = DateTime.Today;
        var defaultWeekStart = weekStart ?? today.AddDays(-(int)today.DayOfWeek);

        var weeklyCalendar = await _scheduleService.GetWeeklyCalendarAsync(defaultWeekStart, user.Id);

        // Get upcoming classes (filter student classes for future dates)
        var allStudentClasses = await _scheduleService.GetStudentClassesAsync(user.Id);
        var upcomingClasses = allStudentClasses
            .Where(c => c.ClassDateTime >= DateTime.UtcNow
                     && c.Status != ClassStatus.Cancelled
                     && !c.IsPendingCheckout) // Exclude pending checkout - those show in cart
            .OrderBy(c => c.ClassDateTime)
            .Take(20)
            .ToList();

        // Get class price for the student
        var classPrice = await _paymentService.GetClassPriceForStudentAsync(user.Id);

        // Check if user has an active subscription
        var activeSubscription = await _subscriptionService.GetActiveSubscriptionAsync(user.Id);

        // Get pending checkout classes (non-subscribers only)
        var pendingClasses = await _classSchedulingService.GetPendingCheckoutAsync(user.Id);
        var pendingClassesList = pendingClasses.ToList();

        // Calculate checkout summary
        var checkoutSummary = await _classSchedulingService.GetCheckoutSummaryAsync(user.Id, 0);

        // Get reserved classes for Preply-style Lessons view (next 2 months)
        var reservedClasses = await _scheduleService.GetReservedClassesAsync(
            user.Id,
            DateTime.Today,
            DateTime.Today.AddMonths(2));

        var viewModel = new MyClassesViewModel
        {
            HasActiveSubscription = activeSubscription != null,
            CurrentSubscription = activeSubscription,
            AvailableTickets = availableTickets,
            WeeklyCalendar = weeklyCalendar,
            UpcomingClasses = upcomingClasses,
            PendingCheckoutClasses = pendingClassesList,
            SubscriptionClassesRemaining = activeSubscription?.ClassesRemainingThisMonth ?? 0,
            CheckoutSummary = checkoutSummary,
            ReservedClasses = reservedClasses.ToList(),
            ActiveTab = tab ?? "lessons"
        };

        return View(viewModel);
    }

    #region Class Scheduling AJAX Endpoints

    /// <summary>
    /// Adds a class to the pending checkout cart (AJAX).
    /// For subscribers: uses subscription allocation by default, or ticket if useTicket=true.
    /// For non-subscribers: adds to checkout cart.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddToCart(int timeSlotId, DateTime classDateTime, bool useTicket = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, error = "Unauthorized" });
        }

        // Validate timeSlotId exists
        var timeSlot = await _context.TimeSlots.FindAsync(timeSlotId);
        if (timeSlot == null)
        {
            return Json(new { success = false, error = "Invalid time slot." });
        }

        // Validate classDateTime is in the future (at least 24 hours from now)
        if (classDateTime <= DateTime.UtcNow.AddHours(24))
        {
            return Json(new { success = false, error = "Classes must be booked at least 24 hours in advance." });
        }

        // Check if user has active subscription
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(user.Id);

        // If subscriber wants to use a ticket for an extra class
        if (subscription != null && useTicket)
        {
            var availableTickets = await _ticketService.GetAvailableTicketCountAsync(user.Id);
            if (availableTickets <= 0)
            {
                return Json(new { success = false, error = "No free class rewards available." });
            }

            // Schedule the class and consume a ticket
            var scheduledClass = await _classSchedulingService.AddClassToCheckoutAsync(
                user.Id, timeSlotId, classDateTime);

            if (scheduledClass == null)
            {
                return Json(new { success = false, error = "Unable to schedule class. The slot may no longer be available." });
            }

            // Use ticket to confirm this class immediately
            var redemption = await _ticketService.UseTicketForFreeClassAsync(user.Id, scheduledClass.Id);
            if (redemption == null)
            {
                // Rollback the pending class
                await _classSchedulingService.RemoveFromCheckoutAsync(scheduledClass.Id, user.Id);
                return Json(new { success = false, error = "Unable to use free class reward." });
            }

            // Mark the class as confirmed (no longer pending)
            scheduledClass.IsPendingCheckout = false;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                confirmed = true,
                usedTicket = true,
                classId = scheduledClass.Id,
                classDateTime = scheduledClass.ClassDateTime.ToString("ddd, MMM d 'at' h:mm tt"),
                ticketsRemaining = availableTickets - 1
            });
        }

        // Normal subscriber flow: schedule immediately with subscription
        if (subscription != null && subscription.ClassesRemainingThisMonth > 0)
        {
            var scheduledClass = await _classSchedulingService.ScheduleWithSubscriptionAsync(
                user.Id, timeSlotId, classDateTime);

            if (scheduledClass == null)
            {
                return Json(new { success = false, error = "Unable to schedule class. The slot may no longer be available." });
            }

            return Json(new
            {
                success = true,
                confirmed = true,
                classId = scheduledClass.Id,
                classDateTime = scheduledClass.ClassDateTime.ToString("ddd, MMM d 'at' h:mm tt"),
                subscriptionClassesRemaining = subscription.ClassesRemainingThisMonth - 1
            });
        }

        // Non-subscriber flow: add to checkout cart
        var pendingClass = await _classSchedulingService.AddClassToCheckoutAsync(
            user.Id, timeSlotId, classDateTime);

        if (pendingClass == null)
        {
            return Json(new { success = false, error = "Unable to add class. The slot may no longer be available." });
        }

        var summary = await _classSchedulingService.GetCheckoutSummaryAsync(user.Id, 0);

        return Json(new
        {
            success = true,
            confirmed = false,
            classId = pendingClass.Id,
            classDateTime = pendingClass.ClassDateTime.ToString("o"), // ISO 8601 format for JavaScript parsing
            summary = new
            {
                classCount = summary.ClassCount,
                perClassPrice = summary.PerClassPrice,
                subtotal = summary.Subtotal,
                totalDue = summary.TotalDue
            }
        });
    }

    /// <summary>
    /// Removes a class from the pending checkout cart (AJAX).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RemoveFromCart(int classId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, error = "Unauthorized" });
        }

        var result = await _classSchedulingService.RemoveFromCheckoutAsync(classId, user.Id);
        if (!result)
        {
            return Json(new { success = false, error = "Unable to remove class from cart." });
        }

        var summary = await _classSchedulingService.GetCheckoutSummaryAsync(user.Id, 0);

        return Json(new
        {
            success = true,
            summary = new
            {
                classCount = summary.ClassCount,
                perClassPrice = summary.PerClassPrice,
                subtotal = summary.Subtotal,
                totalDue = summary.TotalDue
            }
        });
    }

    /// <summary>
    /// Gets the current checkout summary with optional ticket discount (AJAX).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCheckoutSummary(int ticketsToApply = 0)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { error = "Unauthorized" });
        }

        var summary = await _classSchedulingService.GetCheckoutSummaryAsync(user.Id, ticketsToApply);
        var availableTickets = await _ticketService.GetAvailableTicketCountAsync(user.Id);

        return Json(new
        {
            classCount = summary.ClassCount,
            perClassPrice = summary.PerClassPrice,
            subtotal = summary.Subtotal,
            ticketsApplied = summary.TicketsApplied,
            ticketDiscount = summary.TicketDiscount,
            totalDue = summary.TotalDue,
            availableTickets = availableTickets
        });
    }

    /// <summary>
    /// Gets the pending checkout cart items (AJAX).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPendingCart()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { error = "Unauthorized" });
        }

        var pendingClasses = await _classSchedulingService.GetPendingCheckoutAsync(user.Id);

        return Json(pendingClasses.Select(c => new
        {
            id = c.Id,
            dateTime = c.ClassDateTime,
            formattedDateTime = c.ClassDateTime.ToString("ddd, MMM d 'at' h:mm tt"),
            timeSlot = new
            {
                id = c.TimeSlotId,
                startTime = c.TimeSlot.StartTime.ToString(@"hh\:mm"),
                endTime = c.TimeSlot.EndTime.ToString(@"hh\:mm")
            }
        }));
    }

    /// <summary>
    /// Clears all pending checkout items (AJAX).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ClearCart()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, error = "Unauthorized" });
        }

        var clearedCount = await _classSchedulingService.ClearPendingCheckoutAsync(user.Id);

        return Json(new { success = true, clearedCount });
    }

    /// <summary>
    /// Initiates Stripe checkout for pending classes.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(int ticketsToApply = 0)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Validate ticketsToApply is non-negative
        if (ticketsToApply < 0)
        {
            TempData["ErrorMessage"] = "Invalid number of tickets.";
            return RedirectToAction(nameof(MyClasses));
        }

        var successUrl = Url.Action("CheckoutSuccess", "Student", null, Request.Scheme)!;
        var cancelUrl = Url.Action("MyClasses", "Student", null, Request.Scheme)!;

        var checkoutUrl = await _classSchedulingService.CreateCheckoutSessionAsync(
            user.Id, ticketsToApply, successUrl, cancelUrl);

        if (checkoutUrl == null)
        {
            TempData["ErrorMessage"] = "Unable to create checkout session. Please try again.";
            return RedirectToAction(nameof(MyClasses));
        }

        // If it's a free checkout (all covered by tickets), redirect to success
        if (checkoutUrl.Contains("free=true"))
        {
            TempData["SuccessMessage"] = "Classes scheduled successfully using your free class rewards!";
            return RedirectToAction(nameof(MyClasses));
        }

        return Redirect(checkoutUrl);
    }

    /// <summary>
    /// Handle successful checkout return from Stripe.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckoutSuccess(Guid? batchId, int tickets = 0)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // The webhook will handle marking classes as confirmed
        // This just shows the success message
        TempData["SuccessMessage"] = "Your classes have been scheduled successfully!";

        return RedirectToAction(nameof(MyClasses));
    }

    #endregion

    // Legacy BookClass - redirects to MyClasses
    [HttpGet]
    public IActionResult BookClass(DateTime? weekStart)
    {
        return RedirectToAction(nameof(MyClasses), new { weekStart });
    }

    [HttpGet]
    public async Task<IActionResult> GetWeekCalendar(DateTime weekStart)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { error = "Unauthorized" });
        }

        var weeklyCalendar = await _scheduleService.GetWeeklyCalendarAsync(weekStart, user.Id);
        return Json(weeklyCalendar);
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailableDates(int timeSlotId)
    {
        // Validate timeSlotId exists
        var timeSlot = await _context.TimeSlots.FindAsync(timeSlotId);
        if (timeSlot == null)
        {
            return Json(new List<object>());
        }

        var startDate = DateTime.Today;
        var endDate = DateTime.Today.AddDays(60);

        var availableDates = await _scheduleService.GetAvailableDatesAsync(timeSlotId, startDate, endDate);

        return Json(availableDates.Select(d => new
        {
            date = d.ToString("yyyy-MM-dd"),
            time = d.ToString("HH:mm"),
            display = d.ToString("dddd, MMMM d, yyyy 'at' h:mm tt")
        }));
    }

    // Legacy POST BookClass - deprecated in favor of MyClasses checkout flow
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Obsolete("Use MyClasses checkout flow instead. This action now redirects to MyClasses.")]
    public IActionResult BookClass(int timeSlotId, DateTime classDateTime)
    {
        TempData["InfoMessage"] = "Please use the new scheduling page to book classes.";
        return RedirectToAction(nameof(MyClasses));
    }

    // Legacy BookRecurringClasses - deprecated in favor of subscription recurring schedules
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Obsolete("Use subscription recurring schedules instead. This action now redirects to MyClasses.")]
    public IActionResult BookRecurringClasses(int timeSlotId, DateTime startDate, int weekCount)
    {
        TempData["InfoMessage"] = "Recurring classes are now managed through your subscription. Please use the scheduling page.";
        return RedirectToAction(nameof(MyClasses));
    }

    [HttpGet]
    public async Task<IActionResult> GetCancellationStatus(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { error = "Unauthorized" });
        }

        var (canCancel, willForfeitCredit) = await _scheduleService.GetCancellationStatusAsync(id);
        return Json(new { canCancel, willForfeitCredit });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelClass(int id, bool acceptForfeit = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Get class details before cancellation for admin notification
        var scheduledClass = await _context.ScheduledClasses
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.StudentId == user.Id);

        if (scheduledClass == null)
        {
            TempData["ErrorMessage"] = "Unable to cancel this class.";
            return RedirectToAction(nameof(Dashboard));
        }

        var classDateTime = scheduledClass.ClassDateTime;

        // Check if forfeit applies
        var (canCancel, willForfeitCredit) = await _scheduleService.GetCancellationStatusAsync(id);

        if (!canCancel)
        {
            TempData["ErrorMessage"] = "Unable to cancel this class.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (willForfeitCredit && !acceptForfeit)
        {
            TempData["ErrorMessage"] = "Late cancellation requires acceptance of ticket forfeit.";
            return RedirectToAction(nameof(Dashboard));
        }

        var result = await _scheduleService.CancelClassWithForfeitAsync(id, user.Id, false, acceptForfeit);

        if (result)
        {
            // Send admin notification about the cancellation
            var reason = willForfeitCredit ? "Late cancellation (within 24 hours)" : "Cancelled by student";
            await _emailService.SendAdminClassCancelledAsync(
                user.FullName ?? user.Email!,
                user.Email!,
                classDateTime,
                reason);

            if (willForfeitCredit)
            {
                TempData["WarningMessage"] = "Class cancelled. Since this was within 24 hours, your ticket has been forfeited.";
            }
            else
            {
                TempData["SuccessMessage"] = "Class cancelled successfully. Your ticket has been restored.";
            }
        }
        else
        {
            TempData["ErrorMessage"] = "Unable to cancel this class.";
        }

        return RedirectToAction(nameof(Dashboard));
    }

    // Payments
    public async Task<IActionResult> Payments()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var payments = await _paymentService.GetStudentPaymentsAsync(user.Id);

        var viewModel = new PaymentHistoryViewModel
        {
            Payments = payments,
            TotalPaid = payments.Where(p => p.Status == PaymentStatusType.Completed).Sum(p => p.Amount),
            OutstandingBalance = await _paymentService.GetStudentBalanceAsync(user.Id)
        };

        return View(viewModel);
    }

    // Documents
    public async Task<IActionResult> Documents()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var documents = await _context.Documents
            .Where(d => d.IsGlobal || d.StudentDocuments.Any(sd => sd.StudentId == user.Id))
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return View(new StudentDocumentsViewModel { Documents = documents });
    }

    public async Task<IActionResult> DownloadDocument(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id &&
                (d.IsGlobal || d.StudentDocuments.Any(sd => sd.StudentId == user.Id)));

        if (document == null)
        {
            return NotFound();
        }

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", document.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var memory = new MemoryStream();
        using (var stream = new FileStream(filePath, FileMode.Open))
        {
            await stream.CopyToAsync(memory);
        }
        memory.Position = 0;

        return File(memory, GetContentType(document.FileName), document.FileName);
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    // Feedback and Testimonials
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback(SubmitFeedbackViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Verify the class belongs to this student and is completed
        var scheduledClass = await _context.ScheduledClasses
            .FirstOrDefaultAsync(c => c.Id == model.ScheduledClassId
                && c.StudentId == user.Id
                && c.Status == ClassStatus.Completed);

        if (scheduledClass == null)
        {
            TempData["ErrorMessage"] = "Invalid class selection.";
            return RedirectToAction(nameof(Dashboard));
        }

        // Check if feedback already exists
        var existingFeedback = await _context.ClassFeedbacks
            .AnyAsync(f => f.ScheduledClassId == model.ScheduledClassId && f.StudentId == user.Id);

        if (existingFeedback)
        {
            TempData["ErrorMessage"] = "You have already submitted feedback for this class.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please provide a valid rating.";
            return RedirectToAction(nameof(Dashboard));
        }

        // Create the feedback
        var feedback = new Models.Entities.ClassFeedback
        {
            ScheduledClassId = model.ScheduledClassId,
            StudentId = user.Id,
            Rating = model.Rating,
            PrivateComment = model.PrivateComment?.Trim(),
            PublicTestimonial = model.PublicTestimonial?.Trim(),
            AllowPublicDisplay = model.AllowPublicDisplay && !string.IsNullOrWhiteSpace(model.PublicTestimonial),
            IsApproved = false,
            IsFeatured = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ClassFeedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        // Handle optional tip
        if (model.TipAmount.HasValue && model.TipAmount.Value > 0)
        {
            var tipUrl = await _paymentService.CreateTipCheckoutSessionAsync(
                user.Id,
                model.TipAmount.Value,
                model.TipMessage,
                model.ScheduledClassId);

            if (!string.IsNullOrEmpty(tipUrl))
            {
                TempData["SuccessMessage"] = "Thank you for your feedback! Redirecting to complete your tip...";
                return Redirect(tipUrl);
            }
        }

        TempData["SuccessMessage"] = "Thank you for your feedback!";
        return RedirectToAction(nameof(Dashboard));
    }

    // Standalone tip from dashboard
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTip(SendTipViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || model.Amount < 1)
        {
            TempData["ErrorMessage"] = "Please enter a valid tip amount.";
            return RedirectToAction(nameof(Dashboard));
        }

        var tipUrl = await _paymentService.CreateTipCheckoutSessionAsync(
            user.Id,
            model.Amount,
            model.Message,
            model.ScheduledClassId);

        if (string.IsNullOrEmpty(tipUrl))
        {
            TempData["ErrorMessage"] = "Unable to process tip. Please try again.";
            return RedirectToAction(nameof(Dashboard));
        }

        return Redirect(tipUrl);
    }

    // Stripe tip success callback
    [HttpGet]
    public async Task<IActionResult> TipSuccess(string session_id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var success = await _paymentService.ProcessTipWebhookAsync(session_id);

        if (success)
        {
            TempData["SuccessMessage"] = "Thank you for your generous tip! Karen appreciates your support.";
        }
        else
        {
            TempData["WarningMessage"] = "Tip may have been processed. If you have questions, please contact support.";
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public IActionResult TipCancel()
    {
        TempData["InfoMessage"] = "Tip cancelled. No charges were made.";
        return RedirectToAction(nameof(Dashboard));
    }

    // Gamification / Progress
    [HttpGet]
    public async Task<IActionResult> Progress()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Get gamification progress summary
        var progress = await _gamificationService.GetProgressAsync(user.Id);

        // Get all active badges with earned status
        var allBadges = await _gamificationService.GetAllBadgesAsync();
        var studentBadges = await _gamificationService.GetStudentBadgesAsync(user.Id);
        var studentBadgeIds = studentBadges.ToDictionary(sb => sb.BadgeId);

        var badgeViewModels = allBadges.Select(b => new BadgeDisplayViewModel
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description,
            IconUrl = b.IconUrl ?? string.Empty,
            Category = b.Category,
            RequirementType = b.RequirementType,
            RequirementValue = b.RequirementValue,
            RequirementContext = b.RequirementContext,
            BonusPoints = b.BonusPoints,
            DisplayOrder = b.DisplayOrder,
            IsEarned = studentBadgeIds.ContainsKey(b.Id),
            EarnedAt = studentBadgeIds.ContainsKey(b.Id) ? studentBadgeIds[b.Id].EarnedAt : null,
            IsNew = studentBadgeIds.ContainsKey(b.Id) && !studentBadgeIds[b.Id].IsViewed,
            CurrentProgress = GetBadgeProgress(b, user, progress)
        }).OrderByDescending(b => b.IsEarned)
          .ThenBy(b => b.Category)
          .ThenBy(b => b.DisplayOrder)
          .ToList();

        // Get recent point history
        var recentPoints = await _gamificationService.GetPointHistoryAsync(user.Id, 20);
        var pointViewModels = recentPoints.Select(p => new PointTransactionViewModel
        {
            Id = p.Id,
            Source = p.Source,
            BasePoints = p.BasePoints,
            Multiplier = p.Multiplier,
            FinalPoints = p.FinalPoints,
            BalanceAfter = p.BalanceAfter,
            Details = p.Details,
            CreatedAt = p.CreatedAt,
            BadgeName = p.Badge?.Name
        }).ToList();

        var viewModel = new StudentProgressViewModel
        {
            Progress = progress,
            AllBadges = badgeViewModels,
            RecentPointHistory = pointViewModels
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkBadgesViewed()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        await _gamificationService.MarkBadgesAsViewedAsync(user.Id);
        return RedirectToAction(nameof(Progress));
    }

    [HttpGet]
    public async Task<IActionResult> GetNewBadges()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { badges = Array.Empty<object>() });
        }

        var newBadges = await _gamificationService.GetUnviewedBadgesAsync(user.Id);
        var badgeViewModels = newBadges.Select(sb => new
        {
            id = sb.Badge.Id,
            name = sb.Badge.Name,
            description = sb.Badge.Description,
            iconUrl = sb.Badge.IconUrl,
            bonusPoints = sb.Badge.BonusPoints,
            earnedAt = sb.EarnedAt
        }).ToList();

        return Json(new { badges = badgeViewModels });
    }

    [HttpGet]
    public async Task<IActionResult> GetProgressSummary()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { error = "Unauthorized" });
        }

        var progress = await _gamificationService.GetProgressAsync(user.Id);

        return Json(new
        {
            totalPoints = progress.TotalPoints,
            pointsTowardsNextToken = progress.PointsTowardsNextToken,
            tokensEarnedFromPoints = progress.TokensEarnedFromPoints,
            currentStreak = progress.CurrentStreak,
            longestStreak = progress.LongestStreak,
            wasActiveToday = progress.WasActiveToday,
            totalBadgesEarned = progress.TotalBadgesEarned,
            unviewedBadgeCount = progress.UnviewedBadgeCount,
            pointsMultiplier = progress.PointsMultiplier
        });
    }

    private static int? GetBadgeProgress(Badge badge, ApplicationUser user, GamificationProgress progress)
    {
        return badge.RequirementType switch
        {
            BadgeRequirementType.Points => progress.TotalPoints,
            BadgeRequirementType.Streak => progress.CurrentStreak,
            BadgeRequirementType.ClassesCompleted => null, // Would need to query
            BadgeRequirementType.PerfectScores => null, // Would need to query
            _ => null
        };
    }

    #region Token Store

    /// <summary>
    /// Token Store - where students can spend earned tokens on class tickets.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TokenStore()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var viewModel = new TokenStoreViewModel
        {
            TotalTokens = await _tokenService.GetTotalTokenBalanceAsync(user.Id),
            EarnedTokens = await _tokenService.GetEarnedTokenBalanceAsync(user.Id),
            AvailableTickets = await _ticketService.GetAvailableTicketCountAsync(user.Id),
            TicketsBySource = await _ticketService.GetTicketCountsBySourceAsync(user.Id),
            TicketTokenCost = _ticketService.GetTicketTokenCost(),
            CurrentPoints = user.TotalPoints,
            RecentTokenTransactions = await _tokenService.GetTransactionHistoryAsync(user.Id, 10),
            RecentTickets = await _ticketService.GetAllTicketsAsync(user.Id, 10),
            CanPurchaseTickets = await _ticketService.CanPurchaseTicketsAsync(user.Id),
            PurchasePermission = await _ticketService.GetActiveTicketPermissionAsync(user.Id)
        };

        if (TempData["SuccessMessage"] != null)
            viewModel.SuccessMessage = TempData["SuccessMessage"]?.ToString();
        if (TempData["ErrorMessage"] != null)
            viewModel.ErrorMessage = TempData["ErrorMessage"]?.ToString();

        return View(viewModel);
    }

    /// <summary>
    /// Exchange tokens for a class ticket in the Token Store.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyTicketWithTokens()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var tokenCost = _ticketService.GetTicketTokenCost();
        var tokenBalance = await _tokenService.GetTotalTokenBalanceAsync(user.Id);

        if (tokenBalance < tokenCost)
        {
            TempData["ErrorMessage"] = $"You need {tokenCost} tokens to buy a class ticket. You currently have {tokenBalance} tokens.";
            return RedirectToAction(nameof(TokenStore));
        }

        var ticket = await _ticketService.ExchangeTokensForTicketAsync(user.Id);

        if (ticket != null)
        {
            TempData["SuccessMessage"] = $"Success! You exchanged {tokenCost} tokens for a class ticket. You now have {await _ticketService.GetAvailableTicketCountAsync(user.Id)} ticket(s).";
        }
        else
        {
            TempData["ErrorMessage"] = "Unable to complete the exchange. Please try again.";
        }

        return RedirectToAction(nameof(TokenStore));
    }

    /// <summary>
    /// Initiate a Stripe checkout session to purchase tickets with money.
    /// Only available if student has active purchase permission.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseTickets(int quantity = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        if (!await _ticketService.CanPurchaseTicketsAsync(user.Id))
        {
            TempData["ErrorMessage"] = "You don't have permission to purchase tickets at this time.";
            return RedirectToAction(nameof(TokenStore));
        }

        var permission = await _ticketService.GetActiveTicketPermissionAsync(user.Id);
        if (permission == null || quantity > permission.TokensRemaining)
        {
            TempData["ErrorMessage"] = "Invalid purchase request. Please try again.";
            return RedirectToAction(nameof(TokenStore));
        }

        var successUrl = Url.Action("PurchaseSuccess", "Student", null, Request.Scheme)!;
        var cancelUrl = Url.Action("TokenStore", "Student", null, Request.Scheme)!;

        var checkoutUrl = await _ticketService.CreateTicketPurchaseCheckoutAsync(
            user.Id, quantity, successUrl, cancelUrl);

        if (checkoutUrl == null)
        {
            TempData["ErrorMessage"] = "Unable to create checkout session. Please try again.";
            return RedirectToAction(nameof(TokenStore));
        }

        return Redirect(checkoutUrl);
    }

    /// <summary>
    /// Handle successful ticket purchase return from Stripe.
    /// </summary>
    [HttpGet]
    public IActionResult PurchaseSuccess()
    {
        TempData["SuccessMessage"] = "Thank you for your purchase! Your tickets will be available shortly.";
        return RedirectToAction(nameof(TokenStore));
    }

    #endregion
}
