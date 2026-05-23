using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = AppRoles.Teacher)]
public class TeacherController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public TeacherController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var viewModel = new TeacherDashboardViewModel
        {
            TeacherName = user.FullName,
            Email = user.Email ?? string.Empty,
            JoinedDate = user.CreatedAt
        };

        return View(viewModel);
    }
}
