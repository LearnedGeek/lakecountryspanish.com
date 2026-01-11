using System.ComponentModel.DataAnnotations;
using SpanishScheduler.Web.Models.Entities;

namespace SpanishScheduler.Web.Models.ViewModels;

public class StudentDashboardViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public IEnumerable<ScheduledClass> UpcomingClasses { get; set; } = new List<ScheduledClass>();
    public IEnumerable<ScheduledClass> PastClasses { get; set; } = new List<ScheduledClass>();
    public decimal Balance { get; set; }
    public int AvailablePackageClasses { get; set; }
    public IEnumerable<Document> Documents { get; set; } = new List<Document>();
}

public class CreateStudentViewModel
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

    [Display(Name = "Custom Hourly Rate")]
    [Range(0, 1000)]
    public decimal? CustomHourlyRate { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class EditStudentViewModel
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

    [Display(Name = "Custom Hourly Rate")]
    [Range(0, 1000)]
    public decimal? CustomHourlyRate { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password (leave blank to keep current)")]
    public string? NewPassword { get; set; }
}

public class StudentListViewModel
{
    public IEnumerable<StudentListItemViewModel> Students { get; set; } = new List<StudentListItemViewModel>();
    public string? SearchTerm { get; set; }
}

public class StudentListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public decimal? CustomHourlyRate { get; set; }
    public bool IsActive { get; set; }
    public int UpcomingClassCount { get; set; }
    public decimal Balance { get; set; }
}
