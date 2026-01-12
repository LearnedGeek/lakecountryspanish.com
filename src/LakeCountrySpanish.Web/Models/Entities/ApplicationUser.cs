using Microsoft.AspNetCore.Identity;

namespace LakeCountrySpanish.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public decimal? CustomHourlyRate { get; set; }
    public string? ClassroomUrl { get; set; }  // Default Zoom/Meet link for this student
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;  // Force password change on first login

    public string FullName => $"{FirstName} {LastName}";

    // Navigation properties
    public virtual ICollection<ScheduledClass> ScheduledClasses { get; set; } = new List<ScheduledClass>();
    public virtual ICollection<StudentPackage> StudentPackages { get; set; } = new List<StudentPackage>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<StudentDocument> StudentDocuments { get; set; } = new List<StudentDocument>();
}
