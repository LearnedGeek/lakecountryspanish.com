namespace SpanishScheduler.Web.Models.Entities;

public class StudentDocument
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual Document Document { get; set; } = null!;
}
