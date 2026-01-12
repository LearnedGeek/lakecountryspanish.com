using System.ComponentModel.DataAnnotations;

namespace LakeCountrySpanish.Web.Models.ViewModels;

public class SubmitFeedbackViewModel
{
    public int ScheduledClassId { get; set; }

    [Required]
    [Range(1, 5, ErrorMessage = "Please select a rating between 1 and 5 stars.")]
    public int Rating { get; set; }

    [MaxLength(2000)]
    public string? PrivateComment { get; set; }

    [MaxLength(500)]
    public string? PublicTestimonial { get; set; }

    public bool AllowPublicDisplay { get; set; }

    // Optional tip
    [Range(0, 1000)]
    public decimal? TipAmount { get; set; }

    [MaxLength(200)]
    public string? TipMessage { get; set; }
}

public class CompletedClassForFeedbackViewModel
{
    public int ClassId { get; set; }
    public DateTime ClassDateTime { get; set; }
    public string? TeacherNotes { get; set; }
    public bool HasFeedback { get; set; }
}

public class TestimonialDisplayViewModel
{
    public string StudentFirstName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Testimonial { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

public class AdminTestimonialListViewModel
{
    public IEnumerable<AdminTestimonialItemViewModel> Testimonials { get; set; } = Enumerable.Empty<AdminTestimonialItemViewModel>();
}

public class AdminTestimonialItemViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? PrivateComment { get; set; }
    public string PublicTestimonial { get; set; } = string.Empty;
    public DateTime ClassDate { get; set; }
    public bool IsApproved { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendTipViewModel
{
    [Required]
    [Range(1, 1000, ErrorMessage = "Tip amount must be between $1 and $1000.")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? Message { get; set; }

    public int? ScheduledClassId { get; set; }
}
