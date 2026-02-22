using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using System.Security.Claims;

namespace LakeCountrySpanish.Tests.Controllers;

public class AdminControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IPaymentService> _paymentServiceMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IScheduleService> _scheduleServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ITicketService> _ticketServiceMock;
    private readonly Mock<IGamificationService> _gamificationServiceMock;
    private readonly Mock<IAssignmentService> _assignmentServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IAnalyticsService> _analyticsServiceMock;
    private readonly Mock<ILogger<AdminController>> _loggerMock;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _context = TestDbContextFactory.Create();

        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _paymentServiceMock = new Mock<IPaymentService>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _emailServiceMock = new Mock<IEmailService>();
        _scheduleServiceMock = new Mock<IScheduleService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _ticketServiceMock = new Mock<ITicketService>();
        _gamificationServiceMock = new Mock<IGamificationService>();
        _assignmentServiceMock = new Mock<IAssignmentService>();
        _configurationMock = new Mock<IConfiguration>();
        _analyticsServiceMock = new Mock<IAnalyticsService>();
        _loggerMock = new Mock<ILogger<AdminController>>();

        _controller = CreateController();
    }

    private AdminController CreateController()
    {
        var controller = new AdminController(
            _context,
            _userManagerMock.Object,
            _paymentServiceMock.Object,
            _environmentMock.Object,
            _emailServiceMock.Object,
            _scheduleServiceMock.Object,
            _tokenServiceMock.Object,
            _ticketServiceMock.Object,
            _gamificationServiceMock.Object,
            _assignmentServiceMock.Object,
            _configurationMock.Object,
            _analyticsServiceMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        controller.TempData = tempData;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Student Management Tests

    [Fact]
    public async Task Students_ReturnsView_WithStudentList()
    {
        _userManagerMock.Setup(u => u.GetUsersInRoleAsync("Student"))
            .ReturnsAsync(new List<ApplicationUser>
            {
                new() { Id = "1", Email = "alice@test.com", FirstName = "Alice", LastName = "Smith", IsActive = true },
                new() { Id = "2", Email = "bob@test.com", FirstName = "Bob", LastName = "Jones", IsActive = true }
            });
        _paymentServiceMock.Setup(p => p.GetStudentBalanceAsync(It.IsAny<string>()))
            .ReturnsAsync(0m);

        var result = await _controller.Students(null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<StudentListViewModel>(viewResult.Model);
        Assert.Equal(2, model.Students.Count());
    }

    [Fact]
    public async Task Students_FiltersBy_SearchTerm()
    {
        _userManagerMock.Setup(u => u.GetUsersInRoleAsync("Student"))
            .ReturnsAsync(new List<ApplicationUser>
            {
                new() { Id = "1", Email = "alice@test.com", FirstName = "Alice", LastName = "Smith", IsActive = true },
                new() { Id = "2", Email = "bob@test.com", FirstName = "Bob", LastName = "Jones", IsActive = true }
            });
        _paymentServiceMock.Setup(p => p.GetStudentBalanceAsync(It.IsAny<string>()))
            .ReturnsAsync(0m);

        var result = await _controller.Students("alice");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<StudentListViewModel>(viewResult.Model);
        Assert.Single(model.Students);
    }

    [Fact]
    public async Task StudentProfile_ReturnsNotFound_WhenStudentDoesNotExist()
    {
        _userManagerMock.Setup(u => u.FindByIdAsync("nonexistent"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.StudentProfile("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task StudentProfile_ReturnsView_WithStudentData()
    {
        var student = new ApplicationUser
        {
            Id = "student1",
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "Student",
            IsActive = true
        };
        _userManagerMock.Setup(u => u.FindByIdAsync("student1"))
            .ReturnsAsync(student);
        _paymentServiceMock.Setup(p => p.GetStudentBalanceAsync("student1"))
            .ReturnsAsync(50m);
        _configurationMock.Setup(c => c.GetSection("AppSettings:DefaultClassPrice"))
            .Returns(new Mock<IConfigurationSection>().Object);

        var result = await _controller.StudentProfile("student1");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<StudentProfileViewModel>(viewResult.Model);
    }

    [Fact]
    public void CreateStudent_Get_ReturnsView()
    {
        var result = _controller.CreateStudent();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateStudent_Post_ReturnsView_WhenModelInvalid()
    {
        _controller.ModelState.AddModelError("Email", "Required");

        var model = new CreateStudentViewModel();
        var result = await _controller.CreateStudent(model);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateStudent_Post_RedirectsToStudents_WhenSuccessful()
    {
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Student"))
            .ReturnsAsync(IdentityResult.Success);

        var model = new CreateStudentViewModel
        {
            Email = "new@test.com",
            FirstName = "New",
            LastName = "Student",
            Password = "TestPass123!",
            ConfirmPassword = "TestPass123!"
        };

        var result = await _controller.CreateStudent(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Students", redirectResult.ActionName);
        Assert.Contains("SuccessMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task CreateStudent_Post_ReturnsView_WhenCreateFails()
    {
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email already in use" }));

        var model = new CreateStudentViewModel
        {
            Email = "existing@test.com",
            FirstName = "Test",
            LastName = "User",
            Password = "TestPass123!",
            ConfirmPassword = "TestPass123!"
        };

        var result = await _controller.CreateStudent(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
    }

    #endregion

    #region TimeSlot Management Tests

    [Fact]
    public async Task TimeSlots_ReturnsView_WithTimeSlotList()
    {
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _scheduleServiceMock.Setup(s => s.GetScheduledStudentCountForTimeSlotAsync(It.IsAny<int>()))
            .ReturnsAsync(0);
        _scheduleServiceMock.Setup(s => s.CanDeleteTimeSlotAsync(It.IsAny<int>()))
            .ReturnsAsync(true);

        var result = await _controller.TimeSlots();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<ManageTimeSlotsViewModel>(viewResult.Model);
    }

    [Fact]
    public async Task CreateTimeSlot_Post_ReturnsView_WhenModelInvalid()
    {
        _controller.ModelState.AddModelError("StartTime", "Required");

        var result = await _controller.CreateTimeSlot(new TimeSlotViewModel());

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateTimeSlot_Post_CreatesSlot_WhenValid()
    {
        var model = new TimeSlotViewModel
        {
            DayOfWeek = DayOfWeek.Tuesday,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            IsRecurring = true,
            IsActive = true
        };

        _scheduleServiceMock.Setup(s => s.HasOverlappingTimeSlotsAsync(
                It.IsAny<DayOfWeek?>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<int?>()))
            .ReturnsAsync(false);

        var result = await _controller.CreateTimeSlot(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("TimeSlots", redirectResult.ActionName);
        Assert.Equal(1, _context.TimeSlots.Count());
    }

    [Fact]
    public async Task CreateTimeSlot_Post_RejectsOverlap()
    {
        var model = new TimeSlotViewModel
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsRecurring = true,
            IsActive = true
        };

        _scheduleServiceMock.Setup(s => s.HasOverlappingTimeSlotsAsync(
                It.IsAny<DayOfWeek?>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<int?>()))
            .ReturnsAsync(true);

        var result = await _controller.CreateTimeSlot(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
    }

    [Fact]
    public async Task DeleteTimeSlot_ReturnsNotFound_WhenSlotDoesNotExist()
    {
        var result = await _controller.DeleteTimeSlot(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteTimeSlot_DeletesSlot_WhenNoScheduledClasses()
    {
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _scheduleServiceMock.Setup(s => s.CanDeleteTimeSlotAsync(timeSlot.Id))
            .ReturnsAsync(true);

        var result = await _controller.DeleteTimeSlot(timeSlot.Id);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("TimeSlots", redirectResult.ActionName);
        Assert.Equal(0, _context.TimeSlots.Count());
    }

    [Fact]
    public async Task DeleteTimeSlot_PreventsDeleting_WhenHasScheduledClasses()
    {
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _scheduleServiceMock.Setup(s => s.CanDeleteTimeSlotAsync(timeSlot.Id))
            .ReturnsAsync(false);

        var result = await _controller.DeleteTimeSlot(timeSlot.Id);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("ErrorMessage", _controller.TempData.Keys);
        Assert.Equal(1, _context.TimeSlots.Count());
    }

    [Fact]
    public async Task DeactivateTimeSlot_ReturnsNotFound_WhenSlotDoesNotExist()
    {
        var result = await _controller.DeactivateTimeSlot(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeactivateTimeSlot_SetsIsActiveFalse()
    {
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _scheduleServiceMock.Setup(s => s.GetScheduledStudentCountForTimeSlotAsync(timeSlot.Id))
            .ReturnsAsync(0);

        var result = await _controller.DeactivateTimeSlot(timeSlot.Id);

        Assert.False(timeSlot.IsActive);
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("TimeSlots", redirectResult.ActionName);
    }

    [Fact]
    public async Task ReactivateTimeSlot_ReturnsNotFound_WhenSlotDoesNotExist()
    {
        var result = await _controller.ReactivateTimeSlot(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ReactivateTimeSlot_SetsIsActiveTrue()
    {
        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsActive = false
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var result = await _controller.ReactivateTimeSlot(timeSlot.Id);

        Assert.True(timeSlot.IsActive);
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("TimeSlots", redirectResult.ActionName);
    }

    #endregion

    #region Schedule & Class Management Tests

    [Fact]
    public async Task CompleteClass_ReturnsNotFound_WhenClassDoesNotExist()
    {
        var result = await _controller.CompleteClass(999, null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CompleteClass_MarksClassCompleted()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today,
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var result = await _controller.CompleteClass(scheduledClass.Id, "Great class!");

        Assert.Equal(ClassStatus.Completed, scheduledClass.Status);
        Assert.Equal("Great class!", scheduledClass.TeacherNotes);
        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task CancelClass_ReturnsNotFound_WhenClassDoesNotExist()
    {
        var result = await _controller.CancelClass(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CancelClass_CancelsClassAndRestoresPackageCredit()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var package = new Package { Name = "4-Pack", ClassCount = 4, Price = 100m, IsActive = true };
        _context.Packages.Add(package);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var studentPackage = new StudentPackage
        {
            StudentId = student.Id,
            PackageId = package.Id,
            ClassesRemaining = 2,
            PurchaseDate = DateTime.Today
        };
        _context.StudentPackages.Add(studentPackage);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(3),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.PartOfPackage,
            StudentPackageId = studentPackage.Id
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var result = await _controller.CancelClass(scheduledClass.Id);

        Assert.Equal(ClassStatus.Cancelled, scheduledClass.Status);
        Assert.Equal(3, studentPackage.ClassesRemaining);
        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task CancelClassWithNotification_ReturnsNotFound_WhenClassDoesNotExist()
    {
        var result = await _controller.CancelClassWithNotification(999, "Reason", true);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CancelClassWithNotification_SendsEmail_WhenNotifyTrue()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "Student", LastName = "Test" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(3),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var result = await _controller.CancelClassWithNotification(scheduledClass.Id, "Teacher unavailable", true);

        Assert.Equal(ClassStatus.Cancelled, scheduledClass.Status);
        _emailServiceMock.Verify(e => e.SendClassCancelledAsync(
            student.Email!, It.IsAny<string>(), scheduledClass.ClassDateTime, "Teacher unavailable"),
            Times.Once);
    }

    [Fact]
    public async Task CancelClassWithNotification_DoesNotSendEmail_WhenNotifyFalse()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "Student", LastName = "Test" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(3),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var result = await _controller.CancelClassWithNotification(scheduledClass.Id, "Schedule conflict", false);

        _emailServiceMock.Verify(e => e.SendClassCancelledAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region Package Management Tests

    [Fact]
    public async Task Packages_ReturnsView_WithPackageList()
    {
        _context.Packages.Add(new Package { Name = "4-Pack", ClassCount = 4, Price = 100m, IsActive = true });
        _context.Packages.Add(new Package { Name = "8-Pack", ClassCount = 8, Price = 180m, IsActive = true });
        await _context.SaveChangesAsync();

        var result = await _controller.Packages();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Package>>(viewResult.Model);
        Assert.Equal(2, model.Count());
    }

    [Fact]
    public async Task CreatePackage_Post_ReturnsView_WhenModelInvalid()
    {
        _controller.ModelState.AddModelError("Name", "Required");

        var result = await _controller.CreatePackage(new Package());

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreatePackage_Post_CreatesPackage_WhenValid()
    {
        var package = new Package { Name = "Test Pack", ClassCount = 4, Price = 100m, IsActive = true };

        var result = await _controller.CreatePackage(package);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Packages", redirectResult.ActionName);
        Assert.Equal(1, _context.Packages.Count());
    }

    [Fact]
    public async Task EditPackage_Post_ReturnsView_WhenModelInvalid()
    {
        _controller.ModelState.AddModelError("Name", "Required");

        var result = await _controller.EditPackage(new Package());

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task EditPackage_Post_UpdatesPackage_WhenValid()
    {
        var package = new Package { Name = "Original", ClassCount = 4, Price = 100m, IsActive = true };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();
        _context.Entry(package).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updated = new Package { Id = package.Id, Name = "Updated", ClassCount = 8, Price = 180m, IsActive = true };

        var result = await _controller.EditPackage(updated);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Packages", redirectResult.ActionName);
    }

    #endregion

    #region BlockedDates Management Tests

    [Fact]
    public async Task BlockedDates_ReturnsView_WithBlockedDates()
    {
        _context.BlockedDates.Add(new BlockedDate
        {
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(12),
            Reason = "Holiday"
        });
        await _context.SaveChangesAsync();

        var result = await _controller.BlockedDates();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<BlockedDatesViewModel>(viewResult.Model);
    }

    [Fact]
    public async Task CreateBlockedDate_Post_ReturnsView_WhenModelInvalid()
    {
        _controller.ModelState.AddModelError("Reason", "Required");

        var result = await _controller.CreateBlockedDate(new CreateBlockedDateViewModel());

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateBlockedDate_Post_RejectsEndBeforeStart()
    {
        var model = new CreateBlockedDateViewModel
        {
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(5),
            Reason = "Holiday"
        };

        var result = await _controller.CreateBlockedDate(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
    }

    [Fact]
    public async Task CreateBlockedDate_Post_CreatesBlockedDate_WhenValid()
    {
        var model = new CreateBlockedDateViewModel
        {
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(12),
            Reason = "Holiday"
        };

        var result = await _controller.CreateBlockedDate(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("BlockedDates", redirectResult.ActionName);
        Assert.Equal(1, _context.BlockedDates.Count());
    }

    [Fact]
    public async Task CreateBlockedDate_Post_WarnsAboutConflicts()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        _context.ScheduledClasses.Add(new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(11),
            Status = ClassStatus.Scheduled
        });
        await _context.SaveChangesAsync();

        var model = new CreateBlockedDateViewModel
        {
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(12),
            Reason = "Holiday"
        };

        var result = await _controller.CreateBlockedDate(model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("WarningMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task DeleteBlockedDate_ReturnsNotFound_WhenDoesNotExist()
    {
        var result = await _controller.DeleteBlockedDate(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteBlockedDate_DeletesRecord()
    {
        var blockedDate = new BlockedDate
        {
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(12),
            Reason = "Holiday"
        };
        _context.BlockedDates.Add(blockedDate);
        await _context.SaveChangesAsync();

        var result = await _controller.DeleteBlockedDate(blockedDate.Id);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("BlockedDates", redirectResult.ActionName);
        Assert.Equal(0, _context.BlockedDates.Count());
    }

    #endregion

    #region Testimonial Management Tests

    [Fact]
    public async Task ApproveTestimonial_ReturnsNotFound_WhenDoesNotExist()
    {
        var result = await _controller.ApproveTestimonial(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApproveTestimonial_SetsIsApprovedTrue()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(-1),
            Status = ClassStatus.Completed
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var feedback = new ClassFeedback
        {
            StudentId = student.Id,
            ScheduledClassId = scheduledClass.Id,
            Rating = 5,
            PublicTestimonial = "Great teacher!",
            AllowPublicDisplay = true,
            IsApproved = false
        };
        _context.ClassFeedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        var result = await _controller.ApproveTestimonial(feedback.Id);

        Assert.True(feedback.IsApproved);
        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task FeatureTestimonial_ReturnsNotFound_WhenDoesNotExist()
    {
        var result = await _controller.FeatureTestimonial(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task FeatureTestimonial_SetsApprovedAndFeatured()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today,
            Status = ClassStatus.Completed
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var feedback = new ClassFeedback
        {
            StudentId = student.Id,
            ScheduledClassId = scheduledClass.Id,
            Rating = 5,
            PublicTestimonial = "Amazing!",
            AllowPublicDisplay = true,
            IsApproved = false,
            IsFeatured = false
        };
        _context.ClassFeedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        var result = await _controller.FeatureTestimonial(feedback.Id);

        Assert.True(feedback.IsApproved);
        Assert.True(feedback.IsFeatured);
    }

    [Fact]
    public async Task HideTestimonial_ReturnsNotFound_WhenDoesNotExist()
    {
        var result = await _controller.HideTestimonial(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task HideTestimonial_ClearsApprovedAndFeatured()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today,
            Status = ClassStatus.Completed
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var feedback = new ClassFeedback
        {
            StudentId = student.Id,
            ScheduledClassId = scheduledClass.Id,
            Rating = 5,
            PublicTestimonial = "Great!",
            AllowPublicDisplay = true,
            IsApproved = true,
            IsFeatured = true
        };
        _context.ClassFeedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        var result = await _controller.HideTestimonial(feedback.Id);

        Assert.False(feedback.IsApproved);
        Assert.False(feedback.IsFeatured);
    }

    #endregion

    #region Repair & Reconciliation Tests

    [Fact]
    public async Task RepairPendingCheckouts_RedirectsWithInfo_WhenNoPending()
    {
        var result = await _controller.RepairPendingCheckouts();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirectResult.ActionName);
        Assert.Contains("InfoMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task RepairPendingCheckouts_RepairsClassesWithPayments()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var payment = new Payment
        {
            StudentId = student.Id,
            Amount = 30m,
            Status = PaymentStatusType.Completed,
            CompletedAt = DateTime.UtcNow,
            StripeSessionId = "sess_123"
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var pendingClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(3),
            Status = ClassStatus.Scheduled,
            IsPendingCheckout = true
        };
        _context.ScheduledClasses.Add(pendingClass);
        await _context.SaveChangesAsync();

        var result = await _controller.RepairPendingCheckouts();

        Assert.False(pendingClass.IsPendingCheckout);
        Assert.Equal(PaymentStatus.Paid, pendingClass.PaymentStatus);
        Assert.Contains("SuccessMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task RepairPendingCheckouts_DeletesAbandonedCartItems()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var pendingClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.Today.AddDays(3),
            Status = ClassStatus.Scheduled,
            IsPendingCheckout = true
        };
        _context.ScheduledClasses.Add(pendingClass);
        await _context.SaveChangesAsync();

        var result = await _controller.RepairPendingCheckouts();

        Assert.Equal(0, _context.ScheduledClasses.Count());
    }

    [Fact]
    public async Task OrphanedPayments_ReturnsView_WithPayments()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        _context.Payments.Add(new Payment
        {
            StudentId = student.Id,
            Amount = 30m,
            Status = PaymentStatusType.Completed,
            CompletedAt = DateTime.UtcNow,
            StripeSessionId = "sess_orphan"
        });
        await _context.SaveChangesAsync();

        var result = await _controller.OrphanedPayments();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateClassForPayment_RedirectsWithError_WhenPaymentNotFound()
    {
        var result = await _controller.CreateClassForPayment(999, 1, DateTime.Today);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("OrphanedPayments", redirectResult.ActionName);
        Assert.Contains("ErrorMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task CreateClassForPayment_RedirectsWithError_WhenTimeSlotNotFound()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var payment = new Payment
        {
            StudentId = student.Id,
            Amount = 30m,
            Status = PaymentStatusType.Completed,
            StripeSessionId = "sess_123"
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var result = await _controller.CreateClassForPayment(payment.Id, 999, DateTime.Today.AddDays(3));

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("ErrorMessage", _controller.TempData.Keys);
    }

    [Fact]
    public async Task CreateClassForPayment_CreatesClass_WhenValid()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "S", LastName = "T" };
        _context.Users.Add(student);
        var timeSlot = new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsActive = true };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var payment = new Payment
        {
            StudentId = student.Id,
            Amount = 30m,
            Status = PaymentStatusType.Completed,
            StripeSessionId = "sess_123"
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var result = await _controller.CreateClassForPayment(payment.Id, timeSlot.Id, DateTime.Today.AddDays(3));

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Schedule", redirectResult.ActionName);
        Assert.Equal(1, _context.ScheduledClasses.Count());
        var createdClass = _context.ScheduledClasses.First();
        Assert.Equal(PaymentStatus.Paid, createdClass.PaymentStatus);
        Assert.False(createdClass.IsPendingCheckout);
    }

    #endregion
}
