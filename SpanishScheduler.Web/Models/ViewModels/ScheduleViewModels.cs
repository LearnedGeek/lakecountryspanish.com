using System.ComponentModel.DataAnnotations;
using SpanishScheduler.Web.Models.Entities;

namespace SpanishScheduler.Web.Models.ViewModels;

public class BookClassViewModel
{
    public IEnumerable<TimeSlot> AvailableSlots { get; set; } = new List<TimeSlot>();
    public int? SelectedTimeSlotId { get; set; }
    public IEnumerable<DateTime> AvailableDates { get; set; } = new List<DateTime>();
    public DateTime? SelectedDateTime { get; set; }
    public decimal ClassPrice { get; set; }
    public int AvailablePackageClasses { get; set; }
    public IEnumerable<Package> AvailablePackages { get; set; } = new List<Package>();
}

public class TimeSlotViewModel
{
    public int Id { get; set; }

    [Display(Name = "Day of Week")]
    public DayOfWeek? DayOfWeek { get; set; }

    [Required]
    [Display(Name = "Start Time")]
    public TimeSpan StartTime { get; set; }

    [Required]
    [Display(Name = "End Time")]
    public TimeSpan EndTime { get; set; }

    [Display(Name = "Recurring Weekly")]
    public bool IsRecurring { get; set; } = true;

    [Display(Name = "Specific Date")]
    [DataType(DataType.Date)]
    public DateTime? SpecificDate { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class AdminScheduleViewModel
{
    public IEnumerable<ScheduledClass> Classes { get; set; } = new List<ScheduledClass>();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StudentId { get; set; }
    public IEnumerable<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
}

public class ManageTimeSlotsViewModel
{
    public IEnumerable<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
}
