namespace LakeCountrySpanish.Web.Models.Entities;

public enum ClassStatus
{
    Scheduled,
    Completed,
    Cancelled
}

public enum PaymentStatus
{
    Unpaid,
    Paid,
    PartOfPackage,
    PartOfSubscription
}

public class ScheduledClass
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int TimeSlotId { get; set; }
    public DateTime ClassDateTime { get; set; }
    public ClassStatus Status { get; set; } = ClassStatus.Scheduled;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public int? PaymentId { get; set; }
    public int? StudentPackageId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Subscription fields
    /// <summary>
    /// Subscription this class belongs to (if part of subscription)
    /// </summary>
    public int? SubscriptionId { get; set; }

    /// <summary>
    /// True if this class was auto-generated from a recurring schedule
    /// </summary>
    public bool IsFromRecurringSchedule { get; set; }

    /// <summary>
    /// The recurring schedule that generated this class (if applicable)
    /// </summary>
    public int? RecurringScheduleId { get; set; }

    // Class details
    public string? ClassroomUrlOverride { get; set; }  // Override student's default URL for this class
    public string? TeacherNotes { get; set; }          // Notes on student progress after completion
    public bool CreditForfeited { get; set; } = false; // True if cancelled within 24 hours (no refund)

    // Student feedback (after class completion)
    /// <summary>
    /// Student's rating of the class (1-5 stars)
    /// </summary>
    public int? StudentRating { get; set; }

    /// <summary>
    /// Student's private feedback/comment about the class
    /// </summary>
    public string? StudentComment { get; set; }

    /// <summary>
    /// When the student submitted their feedback
    /// </summary>
    public DateTime? FeedbackSubmittedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual TimeSlot TimeSlot { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    public virtual StudentPackage? StudentPackage { get; set; }
    public virtual Subscription? Subscription { get; set; }
    public virtual RecurringSchedule? RecurringSchedule { get; set; }
    public virtual Tip? Tip { get; set; }
}
