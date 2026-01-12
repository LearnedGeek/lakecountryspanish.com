using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LakeCountrySpanish.Web.Models.Entities;

public class Tip
{
    public int Id { get; set; }

    [Required]
    public string StudentId { get; set; } = string.Empty;

    public int? ScheduledClassId { get; set; }  // Null for dashboard/general tips

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public string? Message { get; set; }

    // Stripe
    public string? StripePaymentIntentId { get; set; }
    public string? StripeSessionId { get; set; }
    public bool IsPaid { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual ScheduledClass? ScheduledClass { get; set; }
}
