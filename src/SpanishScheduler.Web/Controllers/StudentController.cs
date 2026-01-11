using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpanishScheduler.Web.Data;
using SpanishScheduler.Web.Models.Entities;
using SpanishScheduler.Web.Models.ViewModels;
using SpanishScheduler.Web.Services;

namespace SpanishScheduler.Web.Controllers;

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

        var viewModel = new StudentDashboardViewModel
        {
            StudentName = user.FirstName,
            UpcomingClasses = classes.Where(c => c.ClassDateTime >= now && c.Status == ClassStatus.Scheduled),
            PastClasses = classes.Where(c => c.ClassDateTime < now || c.Status != ClassStatus.Scheduled).Take(10),
            Balance = await _paymentService.GetStudentBalanceAsync(user.Id),
            AvailablePackageClasses = availablePackageClasses,
            Documents = documents
        };

        return View(viewModel);
    }

    // Scheduling
    [HttpGet]
    public async Task<IActionResult> BookClass()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var timeSlots = await _scheduleService.GetAvailableTimeSlotsAsync();
        var packages = await _context.Packages.Where(p => p.IsActive).OrderBy(p => p.ClassCount).ToListAsync();
        var availablePackageClasses = await _context.StudentPackages
            .Where(sp => sp.StudentId == user.Id && sp.ClassesRemaining > 0)
            .SumAsync(sp => sp.ClassesRemaining);

        var viewModel = new BookClassViewModel
        {
            AvailableSlots = timeSlots,
            ClassPrice = await _paymentService.GetClassPriceForStudentAsync(user.Id),
            AvailablePackageClasses = availablePackageClasses,
            AvailablePackages = packages
        };

        return View(viewModel);
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
    public async Task<IActionResult> BookClass(int timeSlotId, DateTime classDateTime, bool usePackage)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Book the class
        var scheduledClass = await _scheduleService.BookClassAsync(user.Id, timeSlotId, classDateTime);
        if (scheduledClass == null)
        {
            TempData["ErrorMessage"] = "This time slot is no longer available. Please choose another time.";
            return RedirectToAction(nameof(BookClass));
        }

        // Handle payment
        if (usePackage)
        {
            // Use a package credit
            var studentPackage = await _context.StudentPackages
                .Where(sp => sp.StudentId == user.Id && sp.ClassesRemaining > 0)
                .OrderBy(sp => sp.ExpirationDate)
                .FirstOrDefaultAsync();

            if (studentPackage != null)
            {
                studentPackage.ClassesRemaining--;
                scheduledClass.StudentPackageId = studentPackage.Id;
                scheduledClass.PaymentStatus = PaymentStatus.PartOfPackage;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Class booked successfully using your package credit!";
                return RedirectToAction(nameof(Dashboard));
            }
            else
            {
                TempData["ErrorMessage"] = "No package credits available. Please pay for the class.";
            }
        }

        // Redirect to payment
        return RedirectToAction("Checkout", "Payment", new { classId = scheduledClass.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelClass(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var result = await _scheduleService.CancelClassAsync(id, user.Id, false);

        if (result)
        {
            TempData["SuccessMessage"] = "Class cancelled successfully.";
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
}
