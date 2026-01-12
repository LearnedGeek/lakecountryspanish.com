using System.ComponentModel.DataAnnotations;

namespace LakeCountrySpanish.Web.Models.Entities;

public class ClassFeedback
{
    public int Id { get; set; }

    [Required]
    public int ScheduledClassId { get; set; }

    [Required]
    public string StudentId { get; set; } = string.Empty;

    // Feedback
    [Range(1, 5)]
    public int Rating { get; set; }  // 1-5 stars

    public string? PrivateComment { get; set; }  // Only visible to admin

    // Testimonial
    public string? PublicTestimonial { get; set; }
    public bool AllowPublicDisplay { get; set; } = false;  // Student consent
    public bool IsApproved { get; set; } = false;  // Admin approval
    public bool IsFeatured { get; set; } = false;  // Show prominently on homepage

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ScheduledClass ScheduledClass { get; set; } = null!;
    public virtual ApplicationUser Student { get; set; } = null!;
}
