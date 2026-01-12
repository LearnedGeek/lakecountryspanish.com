namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// A recurring class schedule for a subscription (e.g., "Every Tuesday at 6pm").
/// These are used to auto-generate ScheduledClass entries each month.
/// </summary>
public class RecurringSchedule
{
    public int Id { get; set; }

    /// <summary>
    /// The subscription this schedule belongs to
    /// </summary>
    public int SubscriptionId { get; set; }

    /// <summary>
    /// The time slot template this schedule is based on
    /// </summary>
    public int TimeSlotId { get; set; }

    /// <summary>
    /// Day of the week for this recurring class
    /// </summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>
    /// Time of day for the class
    /// </summary>
    public TimeSpan Time { get; set; }

    /// <summary>
    /// Whether this schedule is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this schedule was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this schedule was last modified
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Subscription Subscription { get; set; } = null!;
    public virtual TimeSlot TimeSlot { get; set; } = null!;
    public virtual ICollection<ScheduledClass> ScheduledClasses { get; set; } = new List<ScheduledClass>();
}
