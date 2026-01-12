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
        var packages = await _context.Packages
            .Where(p => p.IsActive)
            .OrderBy(p => p.ClassCount)
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

        ViewBag.Testimonials = testimonials;
        return View(packages);
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
