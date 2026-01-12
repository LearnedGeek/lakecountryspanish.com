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
    PartOfPackage
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

    // Class details
    public string? ClassroomUrlOverride { get; set; }  // Override student's default URL for this class
    public string? TeacherNotes { get; set; }          // Notes on student progress after completion
    public bool CreditForfeited { get; set; } = false; // True if cancelled within 24 hours (no refund)

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual TimeSlot TimeSlot { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    public virtual StudentPackage? StudentPackage { get; set; }
}
