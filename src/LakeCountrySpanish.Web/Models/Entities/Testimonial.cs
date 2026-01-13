namespace LakeCountrySpanish.Web.Models.Entities;

public enum TestimonialStatus
{
    Pending,     // Submitted, awaiting approval
    Approved,    // Approved for public display
    Rejected     // Not approved for display
}

/// <summary>
/// Student testimonials that can be displayed publicly on the website.
/// </summary>
public class Testimonial
{
    public int Id { get; set; }

    /// <summary>
    /// The student who submitted the testimonial
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// The testimonial text content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Overall rating (1-5 stars)
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Approval status for public display
    /// </summary>
    public TestimonialStatus Status { get; set; } = TestimonialStatus.Pending;

    /// <summary>
    /// Display name preference: true = show full name, false = show initials only
    /// </summary>
    public bool ShowFullName { get; set; } = false;

    /// <summary>
    /// Student's location (optional, e.g., "Madison, WI")
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Student's occupation or title (optional, e.g., "Medical Professional")
    /// </summary>
    public string? Occupation { get; set; }

    /// <summary>
    /// How long they've been studying (optional, e.g., "6 months")
    /// </summary>
    public string? StudyDuration { get; set; }

    /// <summary>
    /// Optional: which class prompted this testimonial
    /// </summary>
    public int? RelatedClassId { get; set; }

    /// <summary>
    /// Admin notes about review decision
    /// </summary>
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// Who reviewed the testimonial
    /// </summary>
    public string? ReviewedById { get; set; }

    /// <summary>
    /// When reviewed
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Display order (lower = higher priority)
    /// </summary>
    public int DisplayOrder { get; set; } = 100;

    /// <summary>
    /// Feature this testimonial prominently
    /// </summary>
    public bool IsFeatured { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual ApplicationUser? ReviewedBy { get; set; }
    public virtual ScheduledClass? RelatedClass { get; set; }
}
