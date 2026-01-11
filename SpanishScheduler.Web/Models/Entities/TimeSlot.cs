namespace SpanishScheduler.Web.Models.Entities;

public class TimeSlot
{
    public int Id { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsRecurring { get; set; } = true;
    public DateTime? SpecificDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<ScheduledClass> ScheduledClasses { get; set; } = new List<ScheduledClass>();
}
