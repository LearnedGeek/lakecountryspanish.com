using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Controllers;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Tests.Controllers;

public class HomeControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<HomeController>> _loggerMock;
    private readonly HomeController _controller;

    public HomeControllerTests()
    {
        _context = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<HomeController>>();

        _controller = new HomeController(_loggerMock.Object, _context);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Index Tests

    [Fact]
    public async Task Index_ReturnsView_WithHomeViewModel()
    {
        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<HomeViewModel>(viewResult.Model);
    }

    [Fact]
    public async Task Index_IncludesPricingTiers()
    {
        _context.SubscriptionTiers.Add(new SubscriptionTier
        {
            Name = "Basic",
            ClassesPerMonth = 4,
            MonthlyPrice = 100m,
            IsActive = true,
            DisplayOrder = 1
        });
        await _context.SaveChangesAsync();

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeViewModel>(viewResult.Model);
        Assert.Single(model.SubscriptionTiers);
    }

    [Fact]
    public async Task Index_IncludesFeaturedTestimonials()
    {
        var student = new ApplicationUser { Id = "s1", Email = "s@test.com", FirstName = "Alice", LastName = "S" };
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

        _context.ClassFeedbacks.Add(new ClassFeedback
        {
            StudentId = student.Id,
            ScheduledClassId = scheduledClass.Id,
            Rating = 5,
            PublicTestimonial = "Amazing teacher!",
            AllowPublicDisplay = true,
            IsApproved = true,
            IsFeatured = true
        });
        await _context.SaveChangesAsync();

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeViewModel>(viewResult.Model);
        Assert.Single(model.Testimonials);
    }

    #endregion

    #region Simple Page Tests

    [Fact]
    public void About_ReturnsView()
    {
        var result = _controller.About();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var result = _controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void AreasWeServe_ReturnsView()
    {
        var result = _controller.AreasWeServe();

        Assert.IsType<ViewResult>(result);
    }

    #endregion
}
