using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentService _paymentService;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailService _emailService;
    private readonly IScheduleService _scheduleService;
    private readonly IConfiguration _configuration;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPaymentService paymentService,
        IWebHostEnvironment environment,
        IEmailService emailService,
        IScheduleService scheduleService,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _paymentService = paymentService;
        _environment = environment;
        _emailService = emailService;
        _scheduleService = scheduleService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.Today;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var endOfWeek = today.AddDays(7);
        var threeDaysOut = today.AddDays(3);

        var students = await _userManager.GetUsersInRoleAsync("Student");

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
            HasScheduleConflicts = await _scheduleService.HasScheduleConflictsAsync()
        };

        return View(viewModel);
    }

    // Student Management
    public async Task<IActionResult> Students(string? search)
    {
        var students = await _userManager.GetUsersInRoleAsync("Student");
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
                    .CountAsync(sc => sc.StudentId == student.Id && sc.ClassDateTime >= DateTime.Now && sc.Status == ClassStatus.Scheduled),
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

        var now = DateTime.Now;

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
            await _userManager.AddToRoleAsync(user, "Student");
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
        startDate ??= DateTime.Today;
        endDate ??= DateTime.Today.AddDays(30);

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

        var students = await _userManager.GetUsersInRoleAsync("Student");

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
        var students = await _userManager.GetUsersInRoleAsync("Student");

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
        var students = await _userManager.GetUsersInRoleAsync("Student");
        ViewBag.Students = students.Where(s => s.IsActive).OrderBy(s => s.FullName);
        return View(new UploadDocumentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(UploadDocumentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var students = await _userManager.GetUsersInRoleAsync("Student");
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

        var students = await _userManager.GetUsersInRoleAsync("Student");
        var assignedStudentIds = await _context.StudentDocuments
            .Where(sd => sd.DocumentId == id)
            .Select(sd => sd.StudentId)
            .ToListAsync();

        var model = new AssignDocumentViewModel
        {
            DocumentId = id,
            DocumentTitle = document.Title,
            Students = students.Where(s => s.IsActive).Select(s => new StudentAssignmentViewModel
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

    // Student Credits Management
    [HttpGet]
    public async Task<IActionResult> ManageCredits(string id)
    {
        var student = await _userManager.FindByIdAsync(id);
        if (student == null)
        {
            return NotFound();
        }

        var studentPackages = await _context.StudentPackages
            .Include(sp => sp.Package)
            .Where(sp => sp.StudentId == id)
            .OrderByDescending(sp => sp.PurchaseDate)
            .ToListAsync();

        var totalCredits = studentPackages.Sum(sp => sp.ClassesRemaining);

        return View(new ManageCreditsViewModel
        {
            StudentId = id,
            StudentName = student.FullName,
            StudentEmail = student.Email!,
            StudentPackages = studentPackages,
            TotalCreditsRemaining = totalCredits
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCredits(string studentId, int credits, string reason)
    {
        var student = await _userManager.FindByIdAsync(studentId);
        if (student == null)
        {
            return NotFound();
        }

        if (credits <= 0)
        {
            TempData["ErrorMessage"] = "Credits must be greater than 0.";
            return RedirectToAction(nameof(ManageCredits), new { id = studentId });
        }

        // Create a new "Admin Granted" package entry
        var studentPackage = new StudentPackage
        {
            StudentId = studentId,
            PackageId = 1, // Use single class package as reference
            PurchaseDate = DateTime.UtcNow,
            ClassesRemaining = credits,
            PaymentId = null // No payment - admin granted
        };

        _context.StudentPackages.Add(studentPackage);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Added {credits} credits to {student.FullName}. Reason: {reason}";
        return RedirectToAction(nameof(ManageCredits), new { id = studentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustPackageCredits(int packageId, int newCredits, string reason)
    {
        var studentPackage = await _context.StudentPackages
            .Include(sp => sp.Student)
            .FirstOrDefaultAsync(sp => sp.Id == packageId);

        if (studentPackage == null)
        {
            return NotFound();
        }

        var oldCredits = studentPackage.ClassesRemaining;
        studentPackage.ClassesRemaining = Math.Max(0, newCredits);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Adjusted credits from {oldCredits} to {newCredits} for {studentPackage.Student.FullName}. Reason: {reason}";
        return RedirectToAction(nameof(ManageCredits), new { id = studentPackage.StudentId });
    }

    // Blocked Dates Management (Vacation/Holiday)
    public async Task<IActionResult> BlockedDates()
    {
        var blockedDates = await _context.BlockedDates
            .OrderBy(bd => bd.StartDate)
            .ToListAsync();

        return View(new BlockedDatesViewModel
        {
            BlockedDates = blockedDates,
            UpcomingBlockedDates = blockedDates.Where(bd => bd.EndDate >= DateTime.Today),
            PastBlockedDates = blockedDates.Where(bd => bd.EndDate < DateTime.Today)
        });
    }

    [HttpGet]
    public IActionResult CreateBlockedDate()
    {
        return View(new CreateBlockedDateViewModel
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today
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

        var startDate = DateTime.Today;
        var endDate = DateTime.Today.AddDays(60);
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
}
