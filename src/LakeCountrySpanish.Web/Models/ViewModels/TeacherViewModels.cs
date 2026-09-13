using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LakeCountrySpanish.Web.Models.ViewModels;

public class TeacherDashboardViewModel
{
    public string TeacherName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
}

public class CreateTeacherViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Initial Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class EditTeacherViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password (leave blank to keep current)")]
    public string? NewPassword { get; set; }

    /// <summary>
    /// Roles the admin has checked for this user. Reconciled against the
    /// user's current Identity role set on POST: any role in this list
    /// that isn't currently assigned is added; any role currently assigned
    /// that isn't in this list is removed. Bound from checkboxes on the
    /// edit form; only the admin-visible section produces values.
    /// </summary>
    [Display(Name = "Roles")]
    public List<string> SelectedRoles { get; set; } = new();

    /// <summary>
    /// True when the admin is editing their own account. The role
    /// checkbox section grays out the Admin checkbox and blocks a POST
    /// that would remove the Admin role from the current user — cheap
    /// guard against locking oneself out.
    /// </summary>
    [BindNever]
    public bool IsSelf { get; set; }
}

public class TeacherListViewModel
{
    public IEnumerable<TeacherListItemViewModel> Teachers { get; set; } = new List<TeacherListItemViewModel>();
    public string? SearchTerm { get; set; }
}

public class TeacherListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime JoinedDate { get; set; }
}
