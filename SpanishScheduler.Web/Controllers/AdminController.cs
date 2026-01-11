using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpanishScheduler.Web.Data;
using SpanishScheduler.Web.Models.Entities;
using SpanishScheduler.Web.Models.ViewModels;
using SpanishScheduler.Web.Services;

namespace SpanishScheduler.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentService _paymentService;
    private readonly IWebHostEnvironment _environment;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPaymentService paymentService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _paymentService = paymentService;
        _environment = environment;
    }

    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.Today;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var endOfWeek = today.AddDays(7);

        var students = await _userManager.GetUsersInRoleAsync("Student");

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
            TodaysSchedule = await _context.ScheduledClasses
                .Include(sc => sc.Student)
                .Include(sc => sc.TimeSlot)
                .Where(sc => sc.ClassDateTime.Date == today && sc.Status == ClassStatus.Scheduled)
                .OrderBy(sc => sc.ClassDateTime)
                .ToListAsync(),
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
                .ToListAsync()
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
            IsActive = user.IsActive
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

        return View(new ManageTimeSlotsViewModel { TimeSlots = timeSlots });
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

        // Check if there are scheduled classes
        var hasClasses = await _context.ScheduledClasses
            .AnyAsync(sc => sc.TimeSlotId == id && sc.Status == ClassStatus.Scheduled);

        if (hasClasses)
        {
            TempData["ErrorMessage"] = "Cannot delete time slot with scheduled classes.";
            return RedirectToAction(nameof(TimeSlots));
        }

        timeSlot.IsActive = false;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Time slot deactivated successfully.";
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

        var students = await _userManager.GetUsersInRoleAsync("Student");

        return View(new AdminScheduleViewModel
        {
            Classes = classes,
            StartDate = startDate,
            EndDate = endDate,
            StudentId = studentId,
            Students = students.Where(s => s.IsActive).OrderBy(s => s.FullName)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteClass(int id)
    {
        var scheduledClass = await _context.ScheduledClasses.FindAsync(id);
        if (scheduledClass == null)
        {
            return NotFound();
        }

        scheduledClass.Status = ClassStatus.Completed;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Class marked as completed.";
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
}
