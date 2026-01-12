namespace LakeCountrySpanish.Web.Models.Entities;

public class StudentPackage
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int PackageId { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    public int ClassesRemaining { get; set; }
    public int? PaymentId { get; set; }
    public DateTime? ExpirationDate { get; set; }

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual Package Package { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    public virtual ICollection<ScheduledClass> ScheduledClasses { get; set; } = new List<ScheduledClass>();
}
