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

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual TimeSlot TimeSlot { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    public virtual StudentPackage? StudentPackage { get; set; }
}
