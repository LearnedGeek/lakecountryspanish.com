using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentService _paymentService;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailService _emailService;
    private readonly IScheduleService _scheduleService;
    private readonly ITokenService _tokenService;
    private readonly ITicketService _ticketService;
    private readonly IGamificationService _gamificationService;
    private readonly IAssignmentService _assignmentService;
    private readonly IConfiguration _configuration;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPaymentService paymentService,
        IWebHostEnvironment environment,
        IEmailService emailService,
        IScheduleService scheduleService,
        ITokenService tokenService,
        ITicketService ticketService,
        IGamificationService gamificationService,
        IAssignmentService assignmentService,
        IConfiguration configuration,
        IAnalyticsService analyticsService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _userManager = userManager;
        _paymentService = paymentService;
        _environment = environment;
        _emailService = emailService;
        _scheduleService = scheduleService;
        _tokenService = tokenService;
        _ticketService = ticketService;
        _gamificationService = gamificationService;
        _assignmentService = assignmentService;
        _configuration = configuration;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfWeek = today.AddDays(7);
        var threeDaysOut = today.AddDays(3);

        var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);

        // Get next 3 days of classes
        var upcomingClasses = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Include(sc => sc.TimeSlot)
            .Where(sc => sc.ClassDateTime.Date >= today && sc.ClassDateTime.Date < threeDaysOut && sc.Status == ClassStatus.Scheduled)
            .OrderBy(sc => sc.ClassDateTime)
            .ToListAsync();

        var upcomingDays = new List<DayScheduleGroup>();
        for (int i = 0; i < 3; i++)
        {
            var date = today.AddDays(i);
            upcomingDays.Add(new DayScheduleGroup
            {
                Date = date,
                Classes = upcomingClasses.Where(c => c.ClassDateTime.Date == date).ToList()
            });
        }

        // Get analytics data
        var metrics = await _analyticsService.GetDashboardMetricsAsync();
        var revenueChart = await _analyticsService.GetMonthlyRevenueAsync(6);
        var revenueBreakdown = await _analyticsService.GetRevenueMetricsAsync(startOfMonth, DateTime.UtcNow);
        var subscriptionMetrics = await _analyticsService.GetSubscriptionMetricsAsync();
        var topEngaged = await _analyticsService.GetTopEngagedStudentsAsync(5);
        var atRisk = await _analyticsService.GetAtRiskStudentsAsync(5);
        var difficultyReport = await _analyticsService.GetDifficultyReportAsync();

        var viewModel = new AdminDashboardViewModel
        {
            TotalStudents = students.Count,
            ActiveStudents = students.Count(s => s.IsActive),
            TodaysClasses = await _context.ScheduledClasses
                .CountAsync(sc => sc.ClassDateTime.Date == today && sc.Status == ClassStatus.Scheduled),
            UpcomingClassesThisWeek = await _context.ScheduledClasses
                .CountAsync(sc => sc.ClassDateTime >= today && sc.ClassDateTime <= endOfWeek && sc.Status == ClassStatus.Scheduled),
            TotalRevenueThisMonth = await _context.Payments
                .Where(p => p.CompletedAt >= startOfMonth && p.Status == PaymentStatusType.Completed)
                .SumAsync(p => p.Amount),
            OutstandingPayments = await _context.ScheduledClasses
                .Where(sc => sc.PaymentStatus == PaymentStatus.Unpaid && sc.Status != ClassStatus.Cancelled)
                .CountAsync() * 25m, // Default price, should be calculated properly
            NewInquiries = await _context.ContactInquiries
                .CountAsync(ci => ci.Status == InquiryStatus.New),
            TodaysSchedule = upcomingClasses.Where(c => c.ClassDateTime.Date == today).ToList(),
            RecentInquiries = await _context.ContactInquiries
                .Where(ci => ci.Status == InquiryStatus.New)
                .OrderByDescending(ci => ci.CreatedAt)
                .Take(5)
                .ToListAsync(),
            RecentPayments = await _context.Payments
                .Include(p => p.Student)
                .Where(p => p.Status == PaymentStatusType.Completed)
                .OrderByDescending(p => p.CompletedAt)
                .Take(5)
                .ToListAsync(),
            UpcomingDays = upcomingDays,
            HasScheduleConflicts = await _scheduleService.HasScheduleConflictsAsync(),

            // Analytics data
            Metrics = metrics,
            RevenueChart = revenueChart,
            RevenueBreakdown = revenueBreakdown,
            SubscriptionMetrics = subscriptionMetrics,
            TopEngagedStudents = topEngaged,
            AtRiskStudents = atRisk,
            DifficultyReport = difficultyReport
        };

        return View(viewModel);
    }

    // Student Management
    public async Task<IActionResult> Students(string? search)
    {
        var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);
        var studentList = new List<StudentListItemViewModel>();

        foreach (var student in students)
        {
            if (!string.IsNullOrEmpty(search) &&
                !student.Email!.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !student.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            studentList.Add(new StudentListItemViewModel
            {
                Id = student.Id,
                Email = student.Email!,
                FullName = student.FullName,
                CustomHourlyRate = student.CustomHourlyRate,
                IsActive = student.IsActive,
                UpcomingClassCount = await _context.ScheduledClasses
                    .CountAsync(sc => sc.StudentId == student.Id && sc.ClassDateTime >= DateTime.UtcNow && sc.Status == ClassStatus.Scheduled),
                Balance = await _paymentService.GetStudentBalanceAsync(student.Id)
            });
        }

        return View(new StudentListViewModel
        {
            Students = studentList.OrderBy(s => s.FullName),
            SearchTerm = search
        });
    }

    public async Task<IActionResult> StudentProfile(string id)
    {
        var student = await _userManager.FindByIdAsync(id);
        if (student == null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;

        // Get all classes for this student
        var allClasses = await _context.ScheduledClasses
            .Include(sc => sc.TimeSlot)
            .Where(sc => sc.StudentId == id)
            .OrderByDescending(sc => sc.ClassDateTime)
            .ToListAsync();

        // Get packages
        var packages = await _context.StudentPackages
            .Include(sp => sp.Package)
            .Where(sp => sp.StudentId == id)
            .OrderByDescending(sp => sp.PurchaseDate)
            .ToListAsync();

        // Get payments
        var payments = await _context.Payments
            .Where(p => p.StudentId == id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        // Get documents assigned to this student
        var documents = await _context.Documents
            .Where(d => d.IsGlobal || d.StudentDocuments.Any(sd => sd.StudentId == id))
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        // Get standard rate from config or default
        var standardRate = _configuration.GetValue<decimal>("AppSettings:DefaultClassPrice");
        if (standardRate <= 0) standardRate = 25.00m;

        var viewModel = new StudentProfileViewModel
        {
            Id = student.Id,
            FullName = student.FullName,
            Email = student.Email!,
            IsActive = student.IsActive,
            JoinedDate = student.CreatedAt,
            CustomHourlyRate = student.CustomHourlyRate,
            StandardRate = standardRate,
            ClassroomUrl = student.ClassroomUrl,

            TotalClassesCompleted = allClasses.Count(c => c.Status == ClassStatus.Completed),
            TotalClassesCancelled = allClasses.Count(c => c.Status == ClassStatus.Cancelled),
            UpcomingClassCount = allClasses.Count(c => c.ClassDateTime >= now && c.Status == ClassStatus.Scheduled),
            CreditsRemaining = packages.Where(p => p.ClassesRemaining > 0).Sum(p => p.ClassesRemaining),

            TotalPaid = payments.Where(p => p.Status == PaymentStatusType.Completed).Sum(p => p.Amount),
            Balance = await _paymentService.GetStudentBalanceAsync(id),

            UpcomingClasses = allClasses.Where(c => c.ClassDateTime >= now && c.Status == ClassStatus.Scheduled).Take(10),
            RecentClasses = allClasses.Where(c => c.Status == ClassStatus.Completed).Take(10),
            ActivePackages = packages.Where(p => p.ClassesRemaining > 0),
            RecentPayments = payments.Take(5),
            Documents = documents
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult CreateStudent()
    {
        return View(new CreateStudentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CreateStudent(CreateStudentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            CustomHourlyRate = model.CustomHourlyRate,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, AppRoles.Student);
            TempData["SuccessMessage"] = $"Student {model.FirstName} {model.LastName} has been created successfully.";
            return RedirectToAction(nameof(Students));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditStudent(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        return View(new EditStudentViewModel
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CustomHourlyRate = user.CustomHourlyRate,
            IsActive = user.IsActive,
            ClassroomUrl = user.ClassroomUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> EditStudent(EditStudentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return NotFound();
        }

        user.Email = model.Email;
        user.UserName = model.Email;
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.CustomHourlyRate = model.CustomHourlyRate;
        user.IsActive = model.IsActive;
        user.ClassroomUrl = model.ClassroomUrl;

        var result = await _userManager.UpdateAsync(user);

        if (!string.IsNullOrEmpty(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }
        }

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Student updated successfully.";
            return RedirectToAction(nameof(Students));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // Teacher Management
    public async Task<IActionResult> Teachers(string? search)
    {
        var teachers = await _userManager.GetUsersInRoleAsync(AppRoles.Teacher);
        var teacherList = new List<TeacherListItemViewModel>();

        foreach (var teacher in teachers)
        {
            if (!string.IsNullOrEmpty(search) &&
                !teacher.Email!.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !teacher.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            teacherList.Add(new TeacherListItemViewModel
            {
                Id = teacher.Id,
                Email = teacher.Email!,
                FullName = teacher.FullName,
                IsActive = teacher.IsActive,
                JoinedDate = teacher.CreatedAt
            });
        }

        return View(new TeacherListViewModel
        {
            Teachers = teacherList.OrderBy(t => t.FullName),
            SearchTerm = search
        });
    }

    [HttpGet]
    public IActionResult CreateTeacher()
    {
        return View(new CreateTeacherViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CreateTeacher(CreateTeacherViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true  // Teachers must change the admin-set password on first login
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, AppRoles.Teacher);
            TempData["SuccessMessage"] = $"Teacher {model.FirstName} {model.LastName} has been created successfully.";
            return RedirectToAction(nameof(Teachers));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditTeacher(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Make sure this user is actually a Teacher
        if (!await _userManager.IsInRoleAsync(user, AppRoles.Teacher))
        {
            return NotFound();
        }

        return View(new EditTeacherViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> EditTeacher(EditTeacherViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(user, AppRoles.Teacher))
        {
            return NotFound();
        }

        user.Email = model.Email;
        user.UserName = model.Email;
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.IsActive = model.IsActive;

        var result = await _userManager.UpdateAsync(user);

        if (!string.IsNullOrEmpty(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }
        }

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Teacher updated successfully.";
            return RedirectToAction(nameof(Teachers));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // Time Slot Management
    public async Task<IActionResult> TimeSlots()
    {
        var timeSlots = await _context.TimeSlots
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.StartTime)
            .ToListAsync();

        var viewModels = new List<TimeSlotListItemViewModel>();
        foreach (var slot in timeSlots)
        {
            viewModels.Add(new TimeSlotListItemViewModel
            {
                TimeSlot = slot,
                ScheduledStudentCount = await _scheduleService.GetScheduledStudentCountForTimeSlotAsync(slot.Id),
                CanDelete = await _scheduleService.CanDeleteTimeSlotAsync(slot.Id)
            });
        }

        return View(new ManageTimeSlotsViewModel { TimeSlots = viewModels });
    }

    [HttpGet]
    public IActionResult CreateTimeSlot()
    {
        return View(new TimeSlotViewModel { IsRecurring = true, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTimeSlot(TimeSlotViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Check for overlapping time slots
        if (model.IsRecurring && model.DayOfWeek.HasValue)
        {
            var hasOverlap = await _scheduleService.HasOverlappingTimeSlotsAsync(
                model.DayOfWeek.Value, model.StartTime, model.EndTime);

            if (hasOverlap)
            {
                ModelState.AddModelError(string.Empty, $"This time slot overlaps with an existing slot on {model.DayOfWeek}.");
                return View(model);
            }
        }

        var timeSlot = new TimeSlot
        {
            DayOfWeek = model.IsRecurring ? model.DayOfWeek : null,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            IsRecurring = model.IsRecurring,
            SpecificDate = model.IsRecurring ? null : model.SpecificDate,
            IsActive = model.IsActive
        };

        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Time slot created successfully.";
        return RedirectToAction(nameof(TimeSlots));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteTimeSlot(int id)
    {
        var timeSlot = await _context.TimeSlots.FindAsync(id);
        if (timeSlot == null)
        {
            return NotFound();
        }

        // Only allow deletion if no scheduled classes
        var canDelete = await _scheduleService.CanDeleteTimeSlotAsync(id);
        if (!canDelete)
        {
            TempData["ErrorMessage"] = "Cannot delete time slot with scheduled classes. Use Deactivate instead.";
            return RedirectToAction(nameof(TimeSlots));
        }

        _context.TimeSlots.Remove(timeSlot);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Time slot deleted successfully.";
        return RedirectToAction(nameof(TimeSlots));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateTimeSlot(int id)
    {
        var timeSlot = await _context.TimeSlots.FindAsync(id);
        if (timeSlot == null)
        {
            return NotFound();
        }

        var affectedCount = await _scheduleService.GetScheduledStudentCountForTimeSlotAsync(id);
        timeSlot.IsActive = false;
        await _context.SaveChangesAsync();

        if (affectedCount > 0)
        {
            TempData["WarningMessage"] = $"Time slot deactivated. {affectedCount} scheduled class(es) will remain but no new bookings can be made.";
        }
        else
        {
            TempData["SuccessMessage"] = "Time slot deactivated successfully.";
        }
        return RedirectToAction(nameof(TimeSlots));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateTimeSlot(int id)
    {
        var timeSlot = await _context.TimeSlots.FindAsync(id);
        if (timeSlot == null)
        {
            return NotFound();
        }

        timeSlot.IsActive = true;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Time slot reactivated successfully.";
        return RedirectToAction(nameof(TimeSlots));
    }

    // Schedule Management
    public async Task<IActionResult> Schedule(DateTime? startDate, DateTime? endDate, string? studentId)
    {
        startDate ??= DateTime.UtcNow.Date;
        endDate ??= DateTime.UtcNow.Date.AddDays(30);

        var classes = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Include(sc => sc.TimeSlot)
            .Where(sc => sc.ClassDateTime >= startDate && sc.ClassDateTime <= endDate)
            .Where(sc => string.IsNullOrEmpty(studentId) || sc.StudentId == studentId)
            .OrderBy(sc => sc.ClassDateTime)
            .ToListAsync();

        // Get classes with blocked date conflicts
        var conflictedClasses = await _scheduleService.GetClassesWithBlockedDateConflictsAsync(startDate, endDate);
        var conflictedIds = conflictedClasses.Select(c => c.Id).ToHashSet();

        var classesWithConflicts = classes.Select(c => new ScheduledClassWithConflict
        {
            Class = c,
            HasBlockedDateConflict = conflictedIds.Contains(c.Id)
        }).ToList();

        var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);

        return View(new AdminScheduleViewModel
        {
            Classes = classesWithConflicts,
            StartDate = startDate,
            EndDate = endDate,
            StudentId = studentId,
            Students = students.Where(s => s.IsActive).OrderBy(s => s.FullName),
            HasAnyConflicts = classesWithConflicts.Any(c => c.HasBlockedDateConflict)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteClass(int id, string? teacherNotes)
    {
        var scheduledClass = await _context.ScheduledClasses.FindAsync(id);
        if (scheduledClass == null)
        {
            return NotFound();
        }

        scheduledClass.Status = ClassStatus.Completed;
        scheduledClass.TeacherNotes = teacherNotes;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Class marked as completed.";
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetClassUrl(int id, string? classroomUrl)
    {
        var scheduledClass = await _context.ScheduledClasses.FindAsync(id);
        if (scheduledClass == null)
        {
            return NotFound();
        }

        // Set or clear the override URL
        scheduledClass.ClassroomUrlOverride = string.IsNullOrWhiteSpace(classroomUrl) ? null : classroomUrl.Trim();
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Class URL updated.";
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelClass(int id)
    {
        var scheduledClass = await _context.ScheduledClasses
            .Include(sc => sc.StudentPackage)
            .FirstOrDefaultAsync(sc => sc.Id == id);

        if (scheduledClass == null)
        {
            return NotFound();
        }

        scheduledClass.Status = ClassStatus.Cancelled;

        // Restore package credit if applicable
        if (scheduledClass.PaymentStatus == PaymentStatus.PartOfPackage && scheduledClass.StudentPackage != null)
        {
            scheduledClass.StudentPackage.ClassesRemaining++;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Class cancelled successfully.";
        return RedirectToAction(nameof(Schedule));
    }

    // Payments
    public async Task<IActionResult> Payments(DateTime? startDate, DateTime? endDate, string? studentId)
    {
        var payments = await _paymentService.GetAllPaymentsAsync(startDate, endDate, studentId);
        var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);

        var completedPayments = payments.Where(p => p.Status == PaymentStatusType.Completed);

        return View(new AdminPaymentsViewModel
        {
            Payments = payments,
            StartDate = startDate,
            EndDate = endDate,
            StudentId = studentId,
            Students = students.Where(s => s.IsActive).OrderBy(s => s.FullName),
            TotalRevenue = completedPayments.Sum(p => p.Amount),
            PendingPayments = payments.Where(p => p.Status == PaymentStatusType.Pending).Sum(p => p.Amount)
        });
    }

    // Contact Inquiries
    public async Task<IActionResult> Inquiries(InquiryStatus? status)
    {
        var query = _context.ContactInquiries.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(ci => ci.Status == status.Value);
        }

        var inquiries = await query
            .OrderByDescending(ci => ci.CreatedAt)
            .ToListAsync();

        return View(new AdminInquiriesViewModel
        {
            Inquiries = inquiries,
            FilterStatus = status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInquiryStatus(int id, InquiryStatus status)
    {
        var inquiry = await _context.ContactInquiries.FindAsync(id);
        if (inquiry == null)
        {
            return NotFound();
        }

        inquiry.Status = status;
        if (status == InquiryStatus.Responded)
        {
            inquiry.RespondedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Inquiry status updated.";
        return RedirectToAction(nameof(Inquiries));
    }

    // Documents
    public async Task<IActionResult> Documents()
    {
        var documents = await _context.Documents
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return View(new AdminDocumentsViewModel { Documents = documents });
    }

    [HttpGet]
    public async Task<IActionResult> UploadDocument()
    {
        var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);
        ViewBag.Students = students.Where(s => s.IsActive).OrderBy(s => s.FullName);
        return View(new UploadDocumentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(UploadDocumentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);
            ViewBag.Students = students.Where(s => s.IsActive).OrderBy(s => s.FullName);
            return View(model);
        }

        // Save file
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "documents");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = Guid.NewGuid().ToString() + "_" + model.File.FileName;
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await model.File.CopyToAsync(stream);
        }

        var document = new Document
        {
            Title = model.Title,
            FileName = model.File.FileName,
            FilePath = "/uploads/documents/" + uniqueFileName,
            IsGlobal = model.IsGlobal
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Assign to specific students if not global
        if (!model.IsGlobal && model.SelectedStudentIds.Any())
        {
            foreach (var studentId in model.SelectedStudentIds)
            {
                _context.StudentDocuments.Add(new StudentDocument
                {
                    StudentId = studentId,
                    DocumentId = document.Id
                });
            }
            await _context.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = "Document uploaded successfully.";
        return RedirectToAction(nameof(Documents));
    }

    [HttpGet]
    public async Task<IActionResult> AssignDocument(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
        {
            return NotFound();
        }

        var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);
        var assignedStudentIds = await _context.StudentDocuments
            .Where(sd => sd.DocumentId == id)
            .Select(sd => sd.StudentId)
            .ToListAsync();

        var model = new AssignDocumentViewModel
        {
            DocumentId = id,
            DocumentTitle = document.Title,
            Students = students.Where(s => s.IsActive).Select(s => new StudentDocumentAssignmentViewModel
            {
                StudentId = s.Id,
                StudentName = s.FullName,
                IsAssigned = assignedStudentIds.Contains(s.Id)
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignDocument(int documentId, List<string> studentIds)
    {
        // Remove existing assignments
        var existingAssignments = await _context.StudentDocuments
            .Where(sd => sd.DocumentId == documentId)
            .ToListAsync();
        _context.StudentDocuments.RemoveRange(existingAssignments);

        // Add new assignments
        foreach (var studentId in studentIds)
        {
            _context.StudentDocuments.Add(new StudentDocument
            {
                StudentId = studentId,
                DocumentId = documentId
            });
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Document assignments updated.";
        return RedirectToAction(nameof(Documents));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
        {
            return NotFound();
        }

        // Delete file
        var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Document deleted successfully.";
        return RedirectToAction(nameof(Documents));
    }

    // Packages
    public async Task<IActionResult> Packages()
    {
        var packages = await _context.Packages.OrderBy(p => p.ClassCount).ToListAsync();
        return View(packages);
    }

    [HttpGet]
    public IActionResult CreatePackage()
    {
        return View(new Package { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePackage(Package model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _context.Packages.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Package created successfully.";
        return RedirectToAction(nameof(Packages));
    }

    [HttpGet]
    public async Task<IActionResult> EditPackage(int id)
    {
        var package = await _context.Packages.FindAsync(id);
        if (package == null)
        {
            return NotFound();
        }

        return View(package);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPackage(Package model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _context.Packages.Update(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Package updated successfully.";
        return RedirectToAction(nameof(Packages));
    }

    // Legacy credit management removed - use StudentGamification and GrantTickets instead

    // Blocked Dates Management (Vacation/Holiday)
    public async Task<IActionResult> BlockedDates()
    {
        var blockedDates = await _context.BlockedDates
            .OrderBy(bd => bd.StartDate)
            .ToListAsync();

        return View(new BlockedDatesViewModel
        {
            BlockedDates = blockedDates,
            UpcomingBlockedDates = blockedDates.Where(bd => bd.EndDate >= DateTime.UtcNow.Date),
            PastBlockedDates = blockedDates.Where(bd => bd.EndDate < DateTime.UtcNow.Date)
        });
    }

    [HttpGet]
    public IActionResult CreateBlockedDate()
    {
        return View(new CreateBlockedDateViewModel
        {
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBlockedDate(CreateBlockedDateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.EndDate < model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after start date.");
            return View(model);
        }

        // Check for existing scheduled classes in this period
        var conflictingClasses = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Where(sc => sc.ClassDateTime.Date >= model.StartDate.Date
                      && sc.ClassDateTime.Date <= model.EndDate.Date
                      && sc.Status == ClassStatus.Scheduled)
            .ToListAsync();

        var blockedDate = new BlockedDate
        {
            StartDate = model.StartDate.Date,
            EndDate = model.EndDate.Date,
            Reason = model.Reason
        };

        _context.BlockedDates.Add(blockedDate);
        await _context.SaveChangesAsync();

        if (conflictingClasses.Any())
        {
            TempData["WarningMessage"] = $"Blocked period created. Warning: {conflictingClasses.Count} scheduled class(es) fall within this period and may need to be rescheduled.";
        }
        else
        {
            TempData["SuccessMessage"] = "Blocked period created successfully.";
        }

        return RedirectToAction(nameof(BlockedDates));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteBlockedDate(int id)
    {
        var blockedDate = await _context.BlockedDates.FindAsync(id);
        if (blockedDate == null)
        {
            return NotFound();
        }

        _context.BlockedDates.Remove(blockedDate);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Blocked period deleted successfully.";
        return RedirectToAction(nameof(BlockedDates));
    }

    // Reschedule Class
    [HttpGet]
    public async Task<IActionResult> RescheduleClass(int id)
    {
        var scheduledClass = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Include(sc => sc.TimeSlot)
            .FirstOrDefaultAsync(sc => sc.Id == id);

        if (scheduledClass == null)
        {
            return NotFound();
        }

        if (scheduledClass.Status != ClassStatus.Scheduled)
        {
            TempData["ErrorMessage"] = "Only scheduled classes can be rescheduled.";
            return RedirectToAction(nameof(Schedule));
        }

        var timeSlots = await _context.TimeSlots
            .Where(ts => ts.IsActive)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.StartTime)
            .ToListAsync();

        return View(new RescheduleClassViewModel
        {
            ClassId = scheduledClass.Id,
            StudentName = scheduledClass.Student.FullName,
            StudentEmail = scheduledClass.Student.Email!,
            CurrentDateTime = scheduledClass.ClassDateTime,
            CurrentTimeSlotId = scheduledClass.TimeSlotId,
            AvailableTimeSlots = timeSlots,
            NewDateTime = scheduledClass.ClassDateTime
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleClass(int classId, int timeSlotId, DateTime newDateTime, string reason, bool notifyStudent)
    {
        var scheduledClass = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .FirstOrDefaultAsync(sc => sc.Id == classId);

        if (scheduledClass == null)
        {
            return NotFound();
        }

        // Check if new slot is available
        var isAvailable = !await _context.ScheduledClasses
            .AnyAsync(sc => sc.TimeSlotId == timeSlotId
                         && sc.ClassDateTime.Date == newDateTime.Date
                         && sc.Status != ClassStatus.Cancelled
                         && sc.Id != classId);

        if (!isAvailable)
        {
            TempData["ErrorMessage"] = "The selected time slot is not available on that date.";
            return RedirectToAction(nameof(RescheduleClass), new { id = classId });
        }

        var oldDateTime = scheduledClass.ClassDateTime;
        scheduledClass.TimeSlotId = timeSlotId;
        scheduledClass.ClassDateTime = newDateTime;
        await _context.SaveChangesAsync();

        if (notifyStudent)
        {
            await _emailService.SendClassRescheduledAsync(
                scheduledClass.Student.Email!,
                scheduledClass.Student.FullName,
                oldDateTime,
                newDateTime,
                reason);
            TempData["SuccessMessage"] = $"Class rescheduled from {oldDateTime:MMM d, yyyy h:mm tt} to {newDateTime:MMM d, yyyy h:mm tt}. Student has been notified.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Class rescheduled from {oldDateTime:MMM d, yyyy h:mm tt} to {newDateTime:MMM d, yyyy h:mm tt}.";
        }

        return RedirectToAction(nameof(Schedule));
    }

    // Cancel class with notification option
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelClassWithNotification(int id, string reason, bool notifyStudent)
    {
        var scheduledClass = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Include(sc => sc.StudentPackage)
            .FirstOrDefaultAsync(sc => sc.Id == id);

        if (scheduledClass == null)
        {
            return NotFound();
        }

        scheduledClass.Status = ClassStatus.Cancelled;

        // Restore package credit if applicable
        if (scheduledClass.PaymentStatus == PaymentStatus.PartOfPackage && scheduledClass.StudentPackage != null)
        {
            scheduledClass.StudentPackage.ClassesRemaining++;
        }

        await _context.SaveChangesAsync();

        if (notifyStudent)
        {
            await _emailService.SendClassCancelledAsync(
                scheduledClass.Student.Email!,
                scheduledClass.Student.FullName,
                scheduledClass.ClassDateTime,
                reason);
            TempData["SuccessMessage"] = $"Class cancelled. Credit restored. Student has been notified. Reason: {reason}";
        }
        else
        {
            TempData["SuccessMessage"] = $"Class cancelled. Credit restored. Reason: {reason}";
        }

        return RedirectToAction(nameof(Schedule));
    }

    // Get available dates for a time slot (for rescheduling AJAX)
    [HttpGet]
    public async Task<IActionResult> GetAvailableDatesForSlot(int timeSlotId, int excludeClassId)
    {
        var timeSlot = await _context.TimeSlots.FindAsync(timeSlotId);
        if (timeSlot == null || !timeSlot.IsActive)
        {
            return Json(new List<object>());
        }

        var startDate = DateTime.UtcNow.Date;
        var endDate = DateTime.UtcNow.Date.AddDays(60);
        var blockedDates = await _context.BlockedDates
            .Where(bd => bd.EndDate >= startDate)
            .ToListAsync();

        var bookedDates = await _context.ScheduledClasses
            .Where(sc => sc.TimeSlotId == timeSlotId
                      && sc.ClassDateTime >= startDate
                      && sc.Status != ClassStatus.Cancelled
                      && sc.Id != excludeClassId)
            .Select(sc => sc.ClassDateTime.Date)
            .ToListAsync();

        var availableDates = new List<object>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            // Check if date is blocked
            if (blockedDates.Any(bd => date >= bd.StartDate && date <= bd.EndDate))
            {
                continue;
            }

            // Check if slot matches this date
            bool matches = false;
            if (timeSlot.IsRecurring && timeSlot.DayOfWeek.HasValue)
            {
                matches = date.DayOfWeek == timeSlot.DayOfWeek.Value;
            }
            else if (timeSlot.SpecificDate.HasValue)
            {
                matches = date.Date == timeSlot.SpecificDate.Value.Date;
            }

            if (!matches) continue;

            // Check if already booked
            if (bookedDates.Contains(date)) continue;

            var dateTime = date.Add(timeSlot.StartTime);
            availableDates.Add(new
            {
                date = dateTime.ToString("yyyy-MM-dd"),
                display = dateTime.ToString("dddd, MMMM d, yyyy 'at' h:mm tt")
            });
        }

        return Json(availableDates);
    }

    // Testimonials Management
    public async Task<IActionResult> Testimonials()
    {
        var testimonials = await _context.ClassFeedbacks
            .Include(f => f.Student)
            .Include(f => f.ScheduledClass)
            .Where(f => f.AllowPublicDisplay && !string.IsNullOrEmpty(f.PublicTestimonial))
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new AdminTestimonialItemViewModel
            {
                Id = f.Id,
                StudentName = f.Student.FullName,
                Rating = f.Rating,
                PrivateComment = f.PrivateComment,
                PublicTestimonial = f.PublicTestimonial!,
                ClassDate = f.ScheduledClass.ClassDateTime,
                IsApproved = f.IsApproved,
                IsFeatured = f.IsFeatured,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();

        return View(new AdminTestimonialListViewModel { Testimonials = testimonials });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveTestimonial(int id)
    {
        var feedback = await _context.ClassFeedbacks.FindAsync(id);
        if (feedback == null)
        {
            return NotFound();
        }

        feedback.IsApproved = true;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Testimonial approved and will now be visible on the website.";
        return RedirectToAction(nameof(Testimonials));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FeatureTestimonial(int id)
    {
        var feedback = await _context.ClassFeedbacks.FindAsync(id);
        if (feedback == null)
        {
            return NotFound();
        }

        feedback.IsApproved = true;
        feedback.IsFeatured = true;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Testimonial featured and will be prominently displayed.";
        return RedirectToAction(nameof(Testimonials));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnfeatureTestimonial(int id)
    {
        var feedback = await _context.ClassFeedbacks.FindAsync(id);
        if (feedback == null)
        {
            return NotFound();
        }

        feedback.IsFeatured = false;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Testimonial is no longer featured.";
        return RedirectToAction(nameof(Testimonials));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HideTestimonial(int id)
    {
        var feedback = await _context.ClassFeedbacks.FindAsync(id);
        if (feedback == null)
        {
            return NotFound();
        }

        feedback.IsApproved = false;
        feedback.IsFeatured = false;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Testimonial hidden from public view.";
        return RedirectToAction(nameof(Testimonials));
    }

    // Token Management
    public async Task<IActionResult> TokenPermissions()
    {
        var permissions = await _context.TokenPurchasePermissions
            .Include(p => p.Student)
            .Include(p => p.GrantedBy)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var viewModels = permissions.Select(p => new AdminTokenPermissionViewModel
        {
            Id = p.Id,
            StudentId = p.StudentId,
            StudentName = p.Student.FullName,
            StudentEmail = p.Student.Email!,
            IsEnabled = p.IsEnabled,
            TokenLimit = p.TokenLimit,
            TokensPurchased = p.TokensPurchased,
            TokensRemaining = p.TokensRemaining,
            ExpiresAt = p.ExpiresAt,
            TokenValidityDays = p.TokenValidityDays,
            TokenPrice = p.TokenPrice,
            Reason = p.Reason.ToString(),
            AdminNotes = p.AdminNotes,
            CreatedAt = p.CreatedAt,
            IsActive = p.IsActive
        }).ToList();

        return View(viewModels);
    }

    [HttpGet]
    public async Task<IActionResult> GrantTokenPermission(string? studentId)
    {
        var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);
        ViewBag.Students = students.Where(s => s.IsActive).OrderBy(s => s.FullName);

        return View(new GrantPermissionViewModel
        {
            StudentId = studentId ?? string.Empty,
            TokenLimit = 3,
            ExpirationDays = 30,
            TokenValidityDays = 30,
            TokenPrice = 30.00m,
            Reason = "Trial"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GrantTokenPermission(GrantPermissionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);
            ViewBag.Students = students.Where(s => s.IsActive).OrderBy(s => s.FullName);
            return View(model);
        }

        var student = await _userManager.FindByIdAsync(model.StudentId);
        if (student == null)
        {
            TempData["ErrorMessage"] = "Student not found.";
            return RedirectToAction(nameof(TokenPermissions));
        }

        var adminId = _userManager.GetUserId(User)!;
        var expiresAt = DateTime.UtcNow.AddDays(model.ExpirationDays);
        var reason = Enum.Parse<PermissionReason>(model.Reason);

        await _tokenService.GrantPurchasePermissionAsync(
            model.StudentId,
            model.TokenLimit,
            expiresAt,
            reason,
            model.TokenValidityDays,
            model.TokenPrice,
            adminId,
            model.AdminNotes);

        TempData["SuccessMessage"] = $"Token purchase permission granted to {student.FullName} for {model.TokenLimit} tokens.";
        return RedirectToAction(nameof(TokenPermissions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DisableTokenPermission(int id)
    {
        var result = await _tokenService.DisablePermissionAsync(id);
        if (result)
        {
            TempData["SuccessMessage"] = "Token permission disabled.";
        }
        else
        {
            TempData["ErrorMessage"] = "Permission not found.";
        }
        return RedirectToAction(nameof(TokenPermissions));
    }

    [HttpGet]
    public async Task<IActionResult> GrantTokens(string studentId)
    {
        var student = await _userManager.FindByIdAsync(studentId);
        if (student == null)
        {
            return NotFound();
        }

        ViewBag.StudentName = student.FullName;
        return View(new GrantTokensViewModel
        {
            StudentId = studentId,
            Quantity = 1
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GrantTokens(GrantTokensViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var student = await _userManager.FindByIdAsync(model.StudentId);
            ViewBag.StudentName = student?.FullName ?? "Unknown";
            return View(model);
        }

        var adminId = _userManager.GetUserId(User)!;
        DateTime? expiresAt = model.ExpirationDays.HasValue
            ? DateTime.UtcNow.AddDays(model.ExpirationDays.Value)
            : null;

        await _tokenService.GrantTokensAsync(
            model.StudentId,
            model.Quantity,
            expiresAt,
            adminId,
            model.Notes);

        var studentUser = await _userManager.FindByIdAsync(model.StudentId);
        TempData["SuccessMessage"] = $"Granted {model.Quantity} token(s) to {studentUser?.FullName}.";
        return RedirectToAction(nameof(StudentGamification), new { id = model.StudentId });
    }

    [HttpGet]
    public async Task<IActionResult> StudentTokens(string id)
    {
        var student = await _userManager.FindByIdAsync(id);
        if (student == null)
        {
            return NotFound();
        }

        var earnedBalance = await _tokenService.GetEarnedTokenBalanceAsync(id);
        var purchasedBalance = await _tokenService.GetPurchasedTokenBalanceAsync(id);
        var permission = await _tokenService.GetActivePermissionAsync(id);
        var tokens = await _tokenService.GetActiveTokensAsync(id);
        var transactions = await _tokenService.GetTransactionHistoryAsync(id, 50);
        var permissionHistory = await _tokenService.GetPermissionHistoryAsync(id);

        var viewModel = new StudentTokenSummaryViewModel
        {
            StudentId = id,
            StudentName = student.FullName,
            StudentEmail = student.Email!,
            EarnedTokens = earnedBalance,
            PurchasedTokens = purchasedBalance,
            TotalTokens = earnedBalance + purchasedBalance,
            HasActivePermission = permission != null,
            ActivePermission = permission
        };

        ViewBag.TokenBatches = tokens.Select(t => new TokenBatchViewModel
        {
            Id = t.Id,
            Source = t.Source.ToString(),
            Quantity = t.Quantity,
            QuantityRemaining = t.QuantityRemaining,
            ExpiresAt = t.ExpiresAt,
            CreatedAt = t.CreatedAt
        }).ToList();

        ViewBag.Transactions = transactions.Select(t => new TokenTransactionViewModel
        {
            Id = t.Id,
            Type = t.Type.ToString(),
            Quantity = t.Quantity,
            BalanceAfter = t.BalanceAfter,
            Details = t.Details,
            CreatedAt = t.CreatedAt
        }).ToList();

        ViewBag.PermissionHistory = permissionHistory.Select(p => new AdminTokenPermissionViewModel
        {
            Id = p.Id,
            IsEnabled = p.IsEnabled,
            TokenLimit = p.TokenLimit,
            TokensPurchased = p.TokensPurchased,
            TokensRemaining = p.TokensRemaining,
            ExpiresAt = p.ExpiresAt,
            TokenValidityDays = p.TokenValidityDays,
            TokenPrice = p.TokenPrice,
            Reason = p.Reason.ToString(),
            AdminNotes = p.AdminNotes,
            CreatedAt = p.CreatedAt,
            IsActive = p.IsActive
        }).ToList();

        return View(viewModel);
    }

    /// <summary>
    /// Consolidated gamification management page for a student.
    /// Shows tickets, tokens, badges, points, and engagement metrics.
    /// </summary>
    public async Task<IActionResult> StudentGamification(string id)
    {
        var student = await _userManager.FindByIdAsync(id);
        if (student == null)
        {
            return NotFound();
        }

        // Get ticket data
        var availableTickets = await _ticketService.GetAvailableTicketCountAsync(id);
        var ticketsBySource = await _ticketService.GetTicketCountsBySourceAsync(id);
        var recentTickets = await _ticketService.GetAllTicketsAsync(id, 10);

        // Get token data
        var earnedBalance = await _tokenService.GetEarnedTokenBalanceAsync(id);
        var purchasedBalance = await _tokenService.GetPurchasedTokenBalanceAsync(id);
        var permission = await _tokenService.GetActivePermissionAsync(id);
        var tokens = await _tokenService.GetActiveTokensAsync(id);
        var transactions = await _tokenService.GetTransactionHistoryAsync(id, 20);

        // Get badge data
        var studentBadges = await _context.StudentBadges
            .Include(sb => sb.Badge)
            .Where(sb => sb.StudentId == id)
            .OrderByDescending(sb => sb.EarnedAt)
            .ToListAsync();

        // Get engagement data
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var totalClassesCompleted = await _context.ScheduledClasses
            .CountAsync(c => c.StudentId == id && c.Status == ClassStatus.Completed);
        var classesThisMonth = await _context.ScheduledClasses
            .CountAsync(c => c.StudentId == id && c.Status == ClassStatus.Completed && c.ClassDateTime >= startOfMonth);

        // Get streak data from gamification service
        var progressData = await _gamificationService.GetProgressAsync(id);

        var viewModel = new StudentGamificationViewModel
        {
            StudentId = id,
            StudentName = student.FullName,
            StudentEmail = student.Email!,

            // Tickets
            AvailableTickets = availableTickets,
            TicketsBySource = ticketsBySource,
            RecentTickets = recentTickets,

            // Tokens
            EarnedTokens = earnedBalance,
            PurchasedTokens = purchasedBalance,
            TotalTokens = earnedBalance + purchasedBalance,
            HasActivePermission = permission != null,
            ActivePermission = permission,
            TokenBatches = tokens.Select(t => new TokenBatchViewModel
            {
                Id = t.Id,
                Source = t.Source.ToString(),
                Quantity = t.Quantity,
                QuantityRemaining = t.QuantityRemaining,
                ExpiresAt = t.ExpiresAt,
                CreatedAt = t.CreatedAt
            }).ToList(),
            TokenTransactions = transactions.Select(t => new TokenTransactionViewModel
            {
                Id = t.Id,
                Type = t.Type.ToString(),
                Quantity = t.Quantity,
                BalanceAfter = t.BalanceAfter,
                Details = t.Details,
                CreatedAt = t.CreatedAt
            }).ToList(),

            // Points and engagement
            TotalPoints = student.TotalPoints,
            CurrentStreak = progressData?.CurrentStreak ?? 0,
            LongestStreak = progressData?.LongestStreak ?? 0,
            TotalClassesCompleted = totalClassesCompleted,
            ClassesThisMonth = classesThisMonth,

            // Badges
            Badges = studentBadges.Select(sb => new StudentBadgeViewModel
            {
                BadgeId = sb.BadgeId,
                Name = sb.Badge.Name,
                Description = sb.Badge.Description,
                IconUrl = sb.Badge.IconUrl,
                Emoji = sb.Badge.Emoji,
                Category = sb.Badge.Category,
                EarnedAt = sb.EarnedAt,
                IsNew = !sb.IsViewed
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GrantTickets(string studentId)
    {
        var student = await _userManager.FindByIdAsync(studentId);
        if (student == null)
        {
            return NotFound();
        }

        ViewBag.StudentName = student.FullName;
        return View(new GrantTicketsViewModel
        {
            StudentId = studentId,
            Quantity = 1
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GrantTickets(GrantTicketsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var student = await _userManager.FindByIdAsync(model.StudentId);
            ViewBag.StudentName = student?.FullName ?? "Unknown";
            return View(model);
        }

        DateTime? expiresAt = model.ExpirationDays.HasValue
            ? DateTime.UtcNow.AddDays(model.ExpirationDays.Value)
            : null;

        await _ticketService.GrantTicketsAsync(
            model.StudentId,
            model.Quantity,
            expiresAt,
            model.Notes);

        var studentUser = await _userManager.FindByIdAsync(model.StudentId);
        TempData["SuccessMessage"] = $"Granted {model.Quantity} ticket{(model.Quantity == 1 ? "" : "s")} to {studentUser?.FullName}.";
        return RedirectToAction(nameof(StudentGamification), new { id = model.StudentId });
    }

    // Badge Management
    public async Task<IActionResult> Badges(BadgeCategory? category, bool showInactive = false)
    {
        var badgesQuery = _context.Badges.AsQueryable();

        if (category.HasValue)
        {
            badgesQuery = badgesQuery.Where(b => b.Category == category.Value);
        }

        if (!showInactive)
        {
            badgesQuery = badgesQuery.Where(b => b.IsActive);
        }

        var badges = await badgesQuery
            .OrderBy(b => b.Category)
            .ThenBy(b => b.DisplayOrder)
            .ToListAsync();

        var badgeViewModels = new List<AdminBadgeViewModel>();
        foreach (var badge in badges)
        {
            var earnedCount = await _context.StudentBadges
                .CountAsync(sb => sb.BadgeId == badge.Id);
            var lastEarned = await _context.StudentBadges
                .Where(sb => sb.BadgeId == badge.Id)
                .OrderByDescending(sb => sb.EarnedAt)
                .Select(sb => sb.EarnedAt)
                .FirstOrDefaultAsync();

            badgeViewModels.Add(new AdminBadgeViewModel
            {
                Id = badge.Id,
                Name = badge.Name,
                Description = badge.Description,
                IconUrl = badge.IconUrl ?? string.Empty,
                Category = badge.Category,
                RequirementType = badge.RequirementType,
                RequirementValue = badge.RequirementValue,
                RequirementContext = badge.RequirementContext,
                BonusPoints = badge.BonusPoints,
                IsActive = badge.IsActive,
                DisplayOrder = badge.DisplayOrder,
                TimesEarned = earnedCount,
                LastEarnedAt = lastEarned != default ? lastEarned : null
            });
        }

        return View(new AdminBadgeListViewModel
        {
            Badges = badgeViewModels,
            FilterCategory = category,
            ShowInactive = showInactive
        });
    }

    [HttpGet]
    public IActionResult CreateBadge()
    {
        return View(new BadgeEditViewModel { IsActive = true, DisplayOrder = 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBadge(BadgeEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var badge = new Badge
        {
            Name = model.Name,
            Description = model.Description,
            IconUrl = model.IconUrl,
            Category = model.Category,
            RequirementType = model.RequirementType,
            RequirementValue = model.RequirementValue,
            RequirementContext = model.RequirementContext,
            BonusPoints = model.BonusPoints,
            IsActive = model.IsActive,
            DisplayOrder = model.DisplayOrder
        };

        await _gamificationService.CreateBadgeAsync(badge);

        TempData["SuccessMessage"] = $"Badge '{badge.Name}' created successfully.";
        return RedirectToAction(nameof(Badges));
    }

    [HttpGet]
    public async Task<IActionResult> EditBadge(int id)
    {
        var badge = await _context.Badges.FindAsync(id);
        if (badge == null)
        {
            return NotFound();
        }

        return View(new BadgeEditViewModel
        {
            Id = badge.Id,
            Name = badge.Name,
            Description = badge.Description,
            IconUrl = badge.IconUrl ?? string.Empty,
            Category = badge.Category,
            RequirementType = badge.RequirementType,
            RequirementValue = badge.RequirementValue,
            RequirementContext = badge.RequirementContext,
            BonusPoints = badge.BonusPoints,
            IsActive = badge.IsActive,
            DisplayOrder = badge.DisplayOrder
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBadge(BadgeEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var badge = await _context.Badges.FindAsync(model.Id);
        if (badge == null)
        {
            return NotFound();
        }

        badge.Name = model.Name;
        badge.Description = model.Description;
        badge.IconUrl = model.IconUrl;
        badge.Category = model.Category;
        badge.RequirementType = model.RequirementType;
        badge.RequirementValue = model.RequirementValue;
        badge.RequirementContext = model.RequirementContext;
        badge.BonusPoints = model.BonusPoints;
        badge.IsActive = model.IsActive;
        badge.DisplayOrder = model.DisplayOrder;

        await _gamificationService.UpdateBadgeAsync(badge);

        TempData["SuccessMessage"] = $"Badge '{badge.Name}' updated successfully.";
        return RedirectToAction(nameof(Badges));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableBadge(int id)
    {
        var result = await _gamificationService.DisableBadgeAsync(id);
        if (result)
        {
            TempData["SuccessMessage"] = "Badge disabled. Students who earned it will keep it.";
        }
        else
        {
            TempData["ErrorMessage"] = "Badge not found.";
        }
        return RedirectToAction(nameof(Badges));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableBadge(int id)
    {
        var badge = await _context.Badges.FindAsync(id);
        if (badge == null)
        {
            TempData["ErrorMessage"] = "Badge not found.";
            return RedirectToAction(nameof(Badges));
        }

        badge.IsActive = true;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Badge re-enabled.";
        return RedirectToAction(nameof(Badges));
    }

    [HttpGet]
    public async Task<IActionResult> AwardBadge(string studentId)
    {
        var student = await _userManager.FindByIdAsync(studentId);
        if (student == null)
        {
            return NotFound();
        }

        var earnedBadgeIds = await _context.StudentBadges
            .Where(sb => sb.StudentId == studentId)
            .Select(sb => sb.BadgeId)
            .ToListAsync();

        var availableBadges = await _context.Badges
            .Where(b => b.IsActive && !earnedBadgeIds.Contains(b.Id))
            .OrderBy(b => b.Category)
            .ThenBy(b => b.Name)
            .ToListAsync();

        return View(new AwardBadgeViewModel
        {
            StudentId = studentId,
            StudentName = student.FullName,
            AvailableBadges = availableBadges
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AwardBadge(AwardBadgeViewModel model)
    {
        var result = await _gamificationService.AwardBadgeAsync(model.StudentId, model.BadgeId, model.Context);
        if (result != null)
        {
            var badge = await _context.Badges.FindAsync(model.BadgeId);
            TempData["SuccessMessage"] = $"Badge '{badge?.Name}' awarded successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not award badge. Student may already have it.";
        }
        return RedirectToAction(nameof(StudentGamification), new { id = model.StudentId });
    }

    [HttpGet]
    public async Task<IActionResult> AdjustPoints(string studentId)
    {
        var student = await _userManager.FindByIdAsync(studentId);
        if (student == null)
        {
            return NotFound();
        }

        return View(new AdjustPointsViewModel
        {
            StudentId = studentId,
            StudentName = student.FullName,
            CurrentPoints = student.TotalPoints
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> AdjustPoints(AdjustPointsViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "Reason is required.");
            var student = await _userManager.FindByIdAsync(model.StudentId);
            model.StudentName = student?.FullName ?? "";
            model.CurrentPoints = student?.TotalPoints ?? 0;
            return View(model);
        }

        var adminId = _userManager.GetUserId(User)!;
        await _gamificationService.AdjustPointsAsync(model.StudentId, model.Adjustment, model.Reason, adminId);

        TempData["SuccessMessage"] = $"Points adjusted by {model.Adjustment:+#;-#;0}.";
        return RedirectToAction(nameof(StudentGamification), new { id = model.StudentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetStreaks()
    {
        var count = await _gamificationService.ResetExpiredStreaksAsync();
        TempData["SuccessMessage"] = $"Reset {count} expired streak(s).";
        return RedirectToAction(nameof(Dashboard));
    }

    // Assignment Management

    public async Task<IActionResult> Assignments(string? cefrLevel, AssignmentStatus? status, AssignmentType? type)
    {
        var assignments = await _assignmentService.GetAssignmentsAsync(cefrLevel, status, type);
        var stats = await _assignmentService.GetOverallStatsAsync();

        var viewModels = new List<AdminAssignmentViewModel>();
        foreach (var a in assignments)
        {
            var submissions = await _assignmentService.GetAssignmentSubmissionsAsync(a.Id);
            viewModels.Add(new AdminAssignmentViewModel
            {
                Id = a.Id,
                Title = a.Title,
                Description = a.Description,
                CefrLevel = a.CefrLevel,
                Type = a.Type,
                Status = a.Status,
                TopicName = a.CurriculumTopic?.Name,
                TotalPoints = a.TotalPoints,
                EstimatedMinutes = a.EstimatedMinutes,
                IsAiGenerated = a.IsAiGenerated,
                CreatedAt = a.CreatedAt,
                ReviewedAt = a.ReviewedAt,
                SubmissionCount = submissions.Count(),
                AverageScore = submissions.Any() ? submissions.Average(s => s.PercentageScore) : null
            });
        }

        return View(new AdminAssignmentListViewModel
        {
            Assignments = viewModels,
            Stats = stats,
            FilterCefrLevel = cefrLevel,
            FilterStatus = status,
            FilterType = type
        });
    }

    [HttpGet]
    public async Task<IActionResult> GenerateAssignment(string? cefrLevel, AssignmentType? type, int? topicId)
    {
        var topics = await _assignmentService.GetAllTopicsAsync();

        var viewModel = new GenerateAssignmentViewModel
        {
            CefrLevel = cefrLevel ?? "A1",
            Type = type ?? AssignmentType.MultipleChoice,
            TopicId = topicId,
            AvailableTopics = topics
        };

        // If parameters provided, check for library matches
        if (!string.IsNullOrEmpty(cefrLevel) && type.HasValue)
        {
            viewModel.LibraryMatches = await _assignmentService.FindLibraryMatchesAsync(
                cefrLevel, type.Value, topicId, 5);
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UseFromLibrary(int sourceAssignmentId)
    {
        var adminId = _userManager.GetUserId(User);
        var cloned = await _assignmentService.CloneFromLibraryAsync(sourceAssignmentId, adminId);

        TempData["SuccessMessage"] = "Assignment cloned from library and ready to assign.";
        return RedirectToAction(nameof(AssignToStudents), new { id = cloned.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateAssignment(GenerateAssignmentViewModel model)
    {
        var adminId = _userManager.GetUserId(User);

        // Check library first unless force generation is requested
        if (!model.ForceNewGeneration)
        {
            var libraryMatches = await _assignmentService.FindLibraryMatchesAsync(
                model.CefrLevel, model.Type, model.TopicId, 1);
            var bestMatch = libraryMatches.FirstOrDefault();

            if (bestMatch != null)
            {
                var cloned = await _assignmentService.CloneFromLibraryAsync(bestMatch.Id, adminId);
                TempData["SuccessMessage"] = "Assignment retrieved from library and ready to assign.";
                return RedirectToAction(nameof(AssignToStudents), new { id = cloned.Id });
            }
        }

        // No library match or forced new generation
        var assignment = await _assignmentService.GenerateAssignmentAsync(
            model.CefrLevel,
            model.Type,
            model.TopicId,
            model.QuestionCount,
            model.AdditionalInstructions,
            adminId);

        if (assignment.Status == AssignmentStatus.PendingReview)
        {
            TempData["SuccessMessage"] = "Assignment generated and pending review.";
            return RedirectToAction(nameof(ReviewAssignment), new { id = assignment.Id });
        }
        else
        {
            TempData["WarningMessage"] = "Assignment created as draft. AI generation may have had issues.";
            return RedirectToAction(nameof(EditAssignment), new { id = assignment.Id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CreateAssignment()
    {
        var topics = await _assignmentService.GetAllTopicsAsync();
        return View(new EditAssignmentViewModel
        {
            AvailableTopics = topics
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssignment(EditAssignmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableTopics = await _assignmentService.GetAllTopicsAsync();
            return View(model);
        }

        var adminId = _userManager.GetUserId(User);
        var assignment = new Assignment
        {
            Title = model.Title,
            Description = model.Description,
            CefrLevel = model.CefrLevel,
            CurriculumTopicId = model.CurriculumTopicId,
            Type = model.Type,
            Status = AssignmentStatus.Draft,
            QuestionsJson = model.QuestionsJson,
            AnswersJson = model.AnswersJson,
            TotalPoints = model.TotalPoints,
            BonusPoints = model.BonusPoints,
            EstimatedMinutes = model.EstimatedMinutes,
            CreatedById = adminId
        };

        await _assignmentService.CreateAssignmentAsync(assignment);
        TempData["SuccessMessage"] = "Assignment created.";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpGet]
    public async Task<IActionResult> ViewAssignment(int id)
    {
        var assignment = await _context.Assignments
            .Include(a => a.CurriculumTopic)
            .Include(a => a.CreatedBy)
            .Include(a => a.ReviewedBy)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment == null) return NotFound();

        // Get submission stats
        var submissions = await _context.AssignmentSubmissions
            .Where(s => s.AssignmentId == id)
            .ToListAsync();

        // Get student assignments
        var studentAssignments = await _context.StudentAssignments
            .Include(sa => sa.Student)
            .Where(sa => sa.AssignmentId == id)
            .OrderByDescending(sa => sa.AssignedAt)
            .ToListAsync();

        return View(new ViewAssignmentViewModel
        {
            Id = assignment.Id,
            Title = assignment.Title,
            Description = assignment.Description,
            CefrLevel = assignment.CefrLevel,
            Type = assignment.Type,
            Status = assignment.Status,
            TopicName = assignment.CurriculumTopic?.Name,
            TotalPoints = assignment.TotalPoints,
            BonusPoints = assignment.BonusPoints,
            EstimatedMinutes = assignment.EstimatedMinutes,
            QuestionsJson = assignment.QuestionsJson,
            IsAiGenerated = assignment.IsAiGenerated,
            CreatedByName = assignment.CreatedBy?.FullName,
            CreatedAt = assignment.CreatedAt,
            ReviewedByName = assignment.ReviewedBy?.FullName,
            ReviewedAt = assignment.ReviewedAt,
            ReviewNotes = assignment.ReviewNotes,
            SubmissionCount = submissions.Count,
            AverageScore = submissions.Any() ? submissions.Average(s => s.PercentageScore) : null,
            AssignedStudents = studentAssignments.Select(sa => new AssignedStudentInfo
            {
                StudentName = sa.Student.FullName,
                Status = sa.Status,
                AssignedAt = sa.AssignedAt,
                DueDate = sa.DueDate,
                CompletedAt = sa.CompletedAt,
                BestScore = sa.BestScore
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> EditAssignment(int id)
    {
        var assignment = await _assignmentService.GetAssignmentByIdAsync(id);
        if (assignment == null) return NotFound();

        var topics = await _assignmentService.GetAllTopicsAsync();

        return View(new EditAssignmentViewModel
        {
            Id = assignment.Id,
            Title = assignment.Title,
            Description = assignment.Description,
            CefrLevel = assignment.CefrLevel,
            CurriculumTopicId = assignment.CurriculumTopicId,
            Type = assignment.Type,
            QuestionsJson = assignment.QuestionsJson,
            AnswersJson = assignment.AnswersJson,
            TotalPoints = assignment.TotalPoints,
            BonusPoints = assignment.BonusPoints,
            EstimatedMinutes = assignment.EstimatedMinutes,
            AvailableTopics = topics
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAssignment(EditAssignmentViewModel model)
    {
        if (!ModelState.IsValid || !model.Id.HasValue)
        {
            model.AvailableTopics = await _assignmentService.GetAllTopicsAsync();
            return View(model);
        }

        var assignment = await _assignmentService.GetAssignmentByIdAsync(model.Id.Value);
        if (assignment == null) return NotFound();

        assignment.Title = model.Title;
        assignment.Description = model.Description;
        assignment.CefrLevel = model.CefrLevel;
        assignment.CurriculumTopicId = model.CurriculumTopicId;
        assignment.Type = model.Type;
        assignment.QuestionsJson = model.QuestionsJson;
        assignment.AnswersJson = model.AnswersJson;
        assignment.TotalPoints = model.TotalPoints;
        assignment.BonusPoints = model.BonusPoints;
        assignment.EstimatedMinutes = model.EstimatedMinutes;

        await _assignmentService.UpdateAssignmentAsync(assignment);
        TempData["SuccessMessage"] = "Assignment updated.";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpGet]
    public async Task<IActionResult> ReviewAssignment(int id)
    {
        var assignment = await _assignmentService.GetAssignmentByIdAsync(id);
        if (assignment == null) return NotFound();

        return View(new ReviewAssignmentViewModel
        {
            Id = assignment.Id,
            Title = assignment.Title,
            Description = assignment.Description,
            CefrLevel = assignment.CefrLevel,
            Type = assignment.Type,
            TopicName = assignment.CurriculumTopic?.Name,
            TotalPoints = assignment.TotalPoints,
            EstimatedMinutes = assignment.EstimatedMinutes,
            IsAiGenerated = assignment.IsAiGenerated,
            GenerationPrompt = assignment.GenerationPrompt,
            QuestionsJson = assignment.QuestionsJson,
            AnswersJson = assignment.AnswersJson
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAssignment(int id, string? notes)
    {
        var adminId = _userManager.GetUserId(User)!;
        var result = await _assignmentService.ApproveAssignmentAsync(id, adminId, notes);

        if (result)
        {
            TempData["SuccessMessage"] = "Assignment approved and ready for students.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not approve assignment.";
        }
        return RedirectToAction(nameof(Assignments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAssignment(int id, string? notes)
    {
        var adminId = _userManager.GetUserId(User)!;
        var result = await _assignmentService.RejectAssignmentAsync(id, adminId, notes);

        if (result)
        {
            TempData["SuccessMessage"] = "Assignment rejected.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not reject assignment.";
        }
        return RedirectToAction(nameof(Assignments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveAssignment(int id)
    {
        var result = await _assignmentService.ArchiveAssignmentAsync(id);
        TempData["SuccessMessage"] = result ? "Assignment archived." : "Could not archive assignment.";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpGet]
    public async Task<IActionResult> AssignToStudents(int id)
    {
        var assignment = await _assignmentService.GetAssignmentByIdAsync(id);
        if (assignment == null) return NotFound();

        var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);
        var existingAssignments = await _context.StudentAssignments
            .Where(sa => sa.AssignmentId == id)
            .Select(sa => sa.StudentId)
            .ToListAsync();

        var studentViewModels = students.Where(s => s.IsActive).Select(s => new StudentSelectViewModel
        {
            Id = s.Id,
            Name = s.FullName,
            Email = s.Email!,
            CefrLevel = s.CefrLevel,
            AlreadyAssigned = existingAssignments.Contains(s.Id)
        }).OrderBy(s => s.Name).ToList();

        return View(new AssignToStudentsViewModel
        {
            AssignmentId = id,
            AssignmentTitle = assignment.Title,
            AssignmentDescription = assignment.Description,
            CefrLevel = assignment.CefrLevel,
            AssignmentType = assignment.Type,
            TotalPoints = assignment.TotalPoints,
            AvailableStudents = studentViewModels
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignToStudents(AssignToStudentsViewModel model)
    {
        if (!model.SelectedStudentIds.Any())
        {
            TempData["ErrorMessage"] = "Please select at least one student.";
            return RedirectToAction(nameof(AssignToStudents), new { id = model.AssignmentId });
        }

        var adminId = _userManager.GetUserId(User);
        await _assignmentService.AssignToStudentsAsync(
            model.SelectedStudentIds,
            model.AssignmentId,
            adminId,
            model.DueDate);

        TempData["SuccessMessage"] = $"Assignment sent to {model.SelectedStudentIds.Count} student(s).";
        return RedirectToAction(nameof(Assignments));
    }

    // Curriculum Topics

    public async Task<IActionResult> Topics(string? cefrLevel, TopicType? type)
    {
        var topics = await _assignmentService.GetAllTopicsAsync();

        if (!string.IsNullOrEmpty(cefrLevel))
            topics = topics.Where(t => t.CefrLevel == cefrLevel);

        if (type.HasValue)
            topics = topics.Where(t => t.Type == type.Value);

        return View(new CurriculumTopicListViewModel
        {
            Topics = topics.ToList(),
            FilterCefrLevel = cefrLevel,
            FilterType = type
        });
    }

    [HttpGet]
    public IActionResult CreateTopic()
    {
        return View(new EditTopicViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTopic(EditTopicViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var topic = new CurriculumTopic
        {
            Name = model.Name,
            Description = model.Description,
            CefrLevel = model.CefrLevel,
            Type = model.Type,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            Keywords = model.Keywords,
            ExampleContent = model.ExampleContent
        };

        await _assignmentService.CreateTopicAsync(topic);
        TempData["SuccessMessage"] = "Topic created.";
        return RedirectToAction(nameof(Topics));
    }

    [HttpGet]
    public async Task<IActionResult> EditTopic(int id)
    {
        var topic = await _assignmentService.GetTopicByIdAsync(id);
        if (topic == null) return NotFound();

        return View(new EditTopicViewModel
        {
            Id = topic.Id,
            Name = topic.Name,
            Description = topic.Description,
            CefrLevel = topic.CefrLevel,
            Type = topic.Type,
            DisplayOrder = topic.DisplayOrder,
            IsActive = topic.IsActive,
            Keywords = topic.Keywords,
            ExampleContent = topic.ExampleContent
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTopic(EditTopicViewModel model)
    {
        if (!ModelState.IsValid || !model.Id.HasValue)
        {
            return View(model);
        }

        var topic = await _assignmentService.GetTopicByIdAsync(model.Id.Value);
        if (topic == null) return NotFound();

        topic.Name = model.Name;
        topic.Description = model.Description;
        topic.CefrLevel = model.CefrLevel;
        topic.Type = model.Type;
        topic.DisplayOrder = model.DisplayOrder;
        topic.IsActive = model.IsActive;
        topic.Keywords = model.Keywords;
        topic.ExampleContent = model.ExampleContent;

        await _assignmentService.UpdateTopicAsync(topic);
        TempData["SuccessMessage"] = "Topic updated.";
        return RedirectToAction(nameof(Topics));
    }

    // Difficulty Analysis

    [HttpGet]
    public async Task<IActionResult> DifficultyAnalysis()
    {
        var report = await _analyticsService.GetDifficultyReportAsync();
        var flaggedAssignments = await _analyticsService.GetAssignmentsByDifficultyFeedbackAsync();

        // Separate into too easy and too hard
        var tooEasy = flaggedAssignments.Where(a => a.DominantFeedback == "Too Easy" && a.NeedsAdjustment).ToList();
        var tooHard = flaggedAssignments.Where(a => a.DominantFeedback == "Too Hard" && a.NeedsAdjustment).ToList();

        return View(new DifficultyAnalysisViewModel
        {
            Report = report,
            TooEasyAssignments = tooEasy,
            TooHardAssignments = tooHard,
            AllAssignments = flaggedAssignments
        });
    }

    // Repair orphaned pending checkout classes
    // This fixes classes that were paid for but the webhook didn't confirm them
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> RepairPendingCheckouts()
    {
        // Find all pending checkout classes
        var pendingClasses = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Where(sc => sc.IsPendingCheckout)
            .ToListAsync();

        if (!pendingClasses.Any())
        {
            TempData["InfoMessage"] = "No pending checkout classes found.";
            return RedirectToAction(nameof(Dashboard));
        }

        var repairedCount = 0;
        var deletedCount = 0;

        foreach (var cls in pendingClasses)
        {
            // Check if there's a completed payment for this student
            var payment = await _context.Payments
                .Where(p => p.StudentId == cls.StudentId && p.Status == PaymentStatusType.Completed)
                .OrderByDescending(p => p.CompletedAt)
                .FirstOrDefaultAsync();

            if (payment != null)
            {
                // There's a completed payment - confirm this class
                cls.IsPendingCheckout = false;
                cls.PaymentId = payment.Id;
                cls.PaymentStatus = PaymentStatus.Paid;
                repairedCount++;
            }
            else
            {
                // No payment found - this is an abandoned cart item, delete it
                _context.ScheduledClasses.Remove(cls);
                deletedCount++;
            }
        }

        await _context.SaveChangesAsync();

        var message = $"Repair complete. Confirmed {repairedCount} class(es), removed {deletedCount} abandoned cart item(s).";
        TempData["SuccessMessage"] = message;

        return RedirectToAction(nameof(Dashboard));
    }

    // Find payments that don't have associated scheduled classes (orphaned payments)
    [HttpGet]
    public async Task<IActionResult> OrphanedPayments()
    {
        // Find completed payments that have no scheduled classes linked
        var paymentsWithoutClasses = await _context.Payments
            .Include(p => p.Student)
            .Where(p => p.Status == PaymentStatusType.Completed)
            .Where(p => !_context.ScheduledClasses.Any(sc => sc.PaymentId == p.Id))
            .OrderByDescending(p => p.CompletedAt)
            .ToListAsync();

        // Get active time slots for the scheduling form
        ViewBag.TimeSlots = await _context.TimeSlots
            .Where(ts => ts.IsActive)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.StartTime)
            .Select(ts => new {
                Id = ts.Id,
                Display = ts.IsRecurring && ts.DayOfWeek.HasValue
                    ? $"{ts.DayOfWeek.Value} {ts.StartTime:hh\\:mm} - {ts.EndTime:hh\\:mm}"
                    : $"{ts.SpecificDate:MMM d} {ts.StartTime:hh\\:mm} - {ts.EndTime:hh\\:mm}"
            })
            .ToListAsync();

        return View(paymentsWithoutClasses);
    }

    // Manually create a scheduled class for a student with an orphaned payment
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CreateClassForPayment(int paymentId, int timeSlotId, DateTime classDateTime)
    {
        var payment = await _context.Payments
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
        {
            TempData["ErrorMessage"] = "Payment not found.";
            return RedirectToAction(nameof(OrphanedPayments));
        }

        var timeSlot = await _context.TimeSlots.FindAsync(timeSlotId);
        if (timeSlot == null)
        {
            TempData["ErrorMessage"] = "Time slot not found.";
            return RedirectToAction(nameof(OrphanedPayments));
        }

        // Check if this slot is available
        var isBooked = await _context.ScheduledClasses
            .AnyAsync(sc => sc.TimeSlotId == timeSlotId &&
                           sc.ClassDateTime.Date == classDateTime.Date &&
                           sc.Status != ClassStatus.Cancelled);

        if (isBooked)
        {
            TempData["ErrorMessage"] = "This time slot is already booked for that date.";
            return RedirectToAction(nameof(OrphanedPayments));
        }

        // Combine the date with the time slot's start time
        var actualClassDateTime = classDateTime.Date.Add(timeSlot.StartTime);

        // Create the scheduled class
        var scheduledClass = new ScheduledClass
        {
            StudentId = payment.StudentId,
            TimeSlotId = timeSlotId,
            ClassDateTime = actualClassDateTime,
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Paid,
            PaymentId = payment.Id,
            IsPendingCheckout = false
        };

        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Send class scheduled notification email to student
        if (payment.Student != null && !string.IsNullOrEmpty(payment.Student.Email))
        {
            try
            {
                await _emailService.SendClassScheduledAsync(
                    payment.Student.Email,
                    payment.Student.FirstName ?? payment.Student.FullName ?? payment.Student.Email,
                    actualClassDateTime,
                    payment.Student.ClassroomUrl);
            }
            catch (Exception ex)
            {
                // Log but don't fail the action if email fails
                _logger.LogWarning(ex, "Failed to send class scheduled email to {Email}", payment.Student.Email);
            }
        }

        TempData["SuccessMessage"] = $"Class scheduled for {payment.Student?.FullName ?? "student"} on {actualClassDateTime:MMM d} at {actualClassDateTime:h:mm tt}.";
        return RedirectToAction(nameof(Schedule));
    }
}
