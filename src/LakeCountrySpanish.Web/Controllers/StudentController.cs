using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IScheduleService _scheduleService;
    private readonly IPaymentService _paymentService;

    public StudentController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IScheduleService scheduleService,
        IPaymentService paymentService)
    {
        _context = context;
        _userManager = userManager;
        _scheduleService = scheduleService;
        _paymentService = paymentService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var classes = await _scheduleService.GetStudentClassesAsync(user.Id);
        var now = DateTime.Now;

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

        var viewModel = new StudentDashboardViewModel
        {
            StudentName = user.FirstName,
            DefaultClassroomUrl = user.ClassroomUrl,
            UpcomingClasses = classes.Where(c => c.ClassDateTime >= now && c.Status == ClassStatus.Scheduled),
            PastClasses = classes.Where(c => c.ClassDateTime < now || c.Status != ClassStatus.Scheduled).Take(10),
            Balance = await _paymentService.GetStudentBalanceAsync(user.Id),
            AvailablePackageClasses = availablePackageClasses,
            Documents = documents,
            ClassesNeedingFeedback = classesNeedingFeedback
        };

        return View(viewModel);
    }

    // Purchase Classes - students must buy credits before scheduling
    [HttpGet]
    public async Task<IActionResult> PurchaseClasses()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var packages = await _context.Packages
            .Where(p => p.IsActive)
            .OrderBy(p => p.ClassCount)
            .ToListAsync();

        var availableCredits = await _context.StudentPackages
            .Where(sp => sp.StudentId == user.Id && sp.ClassesRemaining > 0)
            .SumAsync(sp => sp.ClassesRemaining);

        // Get the standard base price (without any custom discount)
        var singleClassPackage = packages.FirstOrDefault(p => p.ClassCount == 1);
        var standardBasePrice = singleClassPackage?.Price ?? 25.00m;

        var viewModel = new PurchaseClassesViewModel
        {
            Packages = packages,
            AvailableCredits = availableCredits,
            BaseClassPrice = standardBasePrice,
            CustomHourlyRate = user.CustomHourlyRate
        };

        return View(viewModel);
    }

    // Scheduling - requires credits (Weekly Calendar View)
    [HttpGet]
    public async Task<IActionResult> BookClass(DateTime? weekStart)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var availablePackageClasses = await _context.StudentPackages
            .Where(sp => sp.StudentId == user.Id && sp.ClassesRemaining > 0)
            .SumAsync(sp => sp.ClassesRemaining);

        // Redirect to purchase page if no credits available
        if (availablePackageClasses == 0)
        {
            TempData["InfoMessage"] = "You need to purchase class credits before you can schedule a lesson.";
            return RedirectToAction(nameof(PurchaseClasses));
        }

        // Default to current week (Sunday start)
        var today = DateTime.Today;
        var defaultWeekStart = weekStart ?? today.AddDays(-(int)today.DayOfWeek);

        var weeklyCalendar = await _scheduleService.GetWeeklyCalendarAsync(defaultWeekStart, user.Id);

        var viewModel = new WeeklyBookingViewModel
        {
            WeeklyCalendar = weeklyCalendar,
            AvailableCredits = availablePackageClasses,
            MaxRecurringWeeks = availablePackageClasses,
            ClassPrice = await _paymentService.GetClassPriceForStudentAsync(user.Id)
        };

        return View(viewModel);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookClass(int timeSlotId, DateTime classDateTime)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Check 1-hour booking restriction
        if (!_scheduleService.CanBookClass(classDateTime))
        {
            TempData["ErrorMessage"] = "Classes must be booked at least 1 hour in advance.";
            return RedirectToAction(nameof(BookClass));
        }

        // Verify student has credits available
        var studentPackage = await _context.StudentPackages
            .Where(sp => sp.StudentId == user.Id && sp.ClassesRemaining > 0)
            .OrderBy(sp => sp.ExpirationDate)
            .ThenBy(sp => sp.PurchaseDate) // Use oldest purchase first
            .FirstOrDefaultAsync();

        if (studentPackage == null)
        {
            TempData["ErrorMessage"] = "You need to purchase class credits before scheduling. Please buy a package first.";
            return RedirectToAction(nameof(PurchaseClasses));
        }

        // Book the class
        var scheduledClass = await _scheduleService.BookClassAsync(user.Id, timeSlotId, classDateTime);
        if (scheduledClass == null)
        {
            TempData["ErrorMessage"] = "This time slot is no longer available. Please choose another time.";
            return RedirectToAction(nameof(BookClass));
        }

        // Deduct credit from package
        studentPackage.ClassesRemaining--;
        scheduledClass.StudentPackageId = studentPackage.Id;
        scheduledClass.PaymentStatus = PaymentStatus.PartOfPackage;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Class booked successfully! You have {studentPackage.ClassesRemaining} credits remaining.";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookRecurringClasses(int timeSlotId, DateTime startDate, int weekCount)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Check 1-hour booking restriction for the first class
        if (!_scheduleService.CanBookClass(startDate))
        {
            TempData["ErrorMessage"] = "The first class must be booked at least 1 hour in advance.";
            return RedirectToAction(nameof(BookClass));
        }

        var availableCredits = await _context.StudentPackages
            .Where(sp => sp.StudentId == user.Id && sp.ClassesRemaining > 0)
            .SumAsync(sp => sp.ClassesRemaining);

        if (weekCount > availableCredits)
        {
            TempData["ErrorMessage"] = $"You only have {availableCredits} credits available. Please select fewer weeks.";
            return RedirectToAction(nameof(BookClass));
        }

        var result = await _scheduleService.BookRecurringClassesAsync(user.Id, timeSlotId, startDate, weekCount);

        if (result.BookedClasses.Any())
        {
            // Deduct credits for booked classes
            var creditsToDeduct = result.CreditsUsed;
            var packages = await _context.StudentPackages
                .Where(sp => sp.StudentId == user.Id && sp.ClassesRemaining > 0)
                .OrderBy(sp => sp.ExpirationDate)
                .ThenBy(sp => sp.PurchaseDate)
                .ToListAsync();

            foreach (var bookedClass in result.BookedClasses)
            {
                var package = packages.FirstOrDefault(p => p.ClassesRemaining > 0);
                if (package != null)
                {
                    package.ClassesRemaining--;
                    bookedClass.StudentPackageId = package.Id;
                    bookedClass.PaymentStatus = PaymentStatus.PartOfPackage;
                }
            }
            await _context.SaveChangesAsync();
        }

        if (result.ConflictDates.Any())
        {
            var conflictMessage = string.Join(", ", result.ConflictDates.Select(d => d.ToString("MMM d")));
            TempData["WarningMessage"] = $"Booked {result.BookedClasses.Count} class(es). Skipped dates with conflicts: {conflictMessage}";
        }
        else if (result.Success)
        {
            TempData["SuccessMessage"] = $"Successfully booked {result.BookedClasses.Count} recurring class(es)!";
        }
        else
        {
            TempData["ErrorMessage"] = "Unable to book any classes. All selected dates have conflicts.";
        }

        return RedirectToAction(nameof(Dashboard));
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

        // Check if forfeit applies
        var (canCancel, willForfeitCredit) = await _scheduleService.GetCancellationStatusAsync(id);

        if (!canCancel)
        {
            TempData["ErrorMessage"] = "Unable to cancel this class.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (willForfeitCredit && !acceptForfeit)
        {
            TempData["ErrorMessage"] = "Late cancellation requires acceptance of credit forfeit.";
            return RedirectToAction(nameof(Dashboard));
        }

        var result = await _scheduleService.CancelClassWithForfeitAsync(id, user.Id, false, acceptForfeit);

        if (result)
        {
            if (willForfeitCredit)
            {
                TempData["WarningMessage"] = "Class cancelled. Since this was within 24 hours, your credit has been forfeited.";
            }
            else
            {
                TempData["SuccessMessage"] = "Class cancelled successfully. Your credit has been restored.";
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
}
