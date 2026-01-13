using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

public class StudentDashboardViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public string? DefaultClassroomUrl { get; set; }  // Student's default Zoom/Meet link
    public IEnumerable<ScheduledClass> UpcomingClasses { get; set; } = new List<ScheduledClass>();
    public IEnumerable<ScheduledClass> PastClasses { get; set; } = new List<ScheduledClass>();
    public decimal Balance { get; set; }
    [Obsolete("Use AvailableTickets instead")]
    public int AvailablePackageClasses { get; set; }
    public int AvailableTickets { get; set; }  // New ticket-based system
    public IEnumerable<Document> Documents { get; set; } = new List<Document>();

    // Completed classes eligible for feedback (completed within last 30 days without existing feedback)
    public IEnumerable<CompletedClassForFeedbackViewModel> ClassesNeedingFeedback { get; set; } = new List<CompletedClassForFeedbackViewModel>();

    // CEFR Level information
    public string? CefrLevel { get; set; }
    public bool HasTakenPlacementTest { get; set; }

    // Gamification quick stats
    public int TotalTokens { get; set; }
    public int PointsToNextToken { get; set; }
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

    [Url]
    [Display(Name = "Classroom URL")]
    public string? ClassroomUrl { get; set; }

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

public class PurchaseClassesViewModel
{
    public IEnumerable<Package> Packages { get; set; } = new List<Package>();
    public int AvailableCredits { get; set; }
    public decimal BaseClassPrice { get; set; }
    public decimal? CustomHourlyRate { get; set; }  // Special rate from instructor
    public bool HasCustomRate => CustomHourlyRate.HasValue && CustomHourlyRate.Value < BaseClassPrice;
}

public class StudentProfileViewModel
{
    // Basic Info
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime JoinedDate { get; set; }

    // Pricing
    public decimal? CustomHourlyRate { get; set; }
    public decimal StandardRate { get; set; }
    public bool HasCustomRate => CustomHourlyRate.HasValue;

    // Class Stats
    public int TotalClassesCompleted { get; set; }
    public int TotalClassesCancelled { get; set; }
    public int CreditsRemaining { get; set; }
    public int UpcomingClassCount { get; set; }

    // Financial
    public decimal TotalPaid { get; set; }
    public decimal Balance { get; set; }

    // Classroom
    public string? ClassroomUrl { get; set; }

    // Lists
    public IEnumerable<ScheduledClass> UpcomingClasses { get; set; } = new List<ScheduledClass>();
    public IEnumerable<ScheduledClass> RecentClasses { get; set; } = new List<ScheduledClass>();
    public IEnumerable<StudentPackage> ActivePackages { get; set; } = new List<StudentPackage>();
    public IEnumerable<Payment> RecentPayments { get; set; } = new List<Payment>();
    public IEnumerable<Document> Documents { get; set; } = new List<Document>();
}
