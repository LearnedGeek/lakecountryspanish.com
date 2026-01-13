using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Load subscription tiers for pricing display
        var tiers = await _context.SubscriptionTiers
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new HomeSubscriptionTierViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                ClassesPerMonth = t.ClassesPerMonth,
                MonthlyPrice = t.MonthlyPrice,
                DisplayOrder = t.DisplayOrder,
                // Mark 8 classes/month (2x/week) as popular, 12 classes (3x/week) as best value
                IsPopular = t.ClassesPerMonth == 8,
                IsBestValue = t.ClassesPerMonth == 12
            })
            .ToListAsync();

        // Load approved testimonials, prioritizing featured ones
        var testimonials = await _context.ClassFeedbacks
            .Include(f => f.Student)
            .Where(f => f.AllowPublicDisplay && f.IsApproved && !string.IsNullOrEmpty(f.PublicTestimonial))
            .OrderByDescending(f => f.IsFeatured)
            .ThenByDescending(f => f.Rating)
            .ThenByDescending(f => f.CreatedAt)
            .Take(6)
            .Select(f => new TestimonialDisplayViewModel
            {
                StudentFirstName = f.Student.FirstName,
                Rating = f.Rating,
                Testimonial = f.PublicTestimonial!,
                Date = f.CreatedAt
            })
            .ToListAsync();

        var viewModel = new HomeViewModel
        {
            SubscriptionTiers = tiers,
            Testimonials = testimonials
        };

        return View(viewModel);
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
