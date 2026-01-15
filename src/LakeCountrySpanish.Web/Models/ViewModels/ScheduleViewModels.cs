using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

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
    public IEnumerable<ScheduledClassWithConflict> Classes { get; set; } = new List<ScheduledClassWithConflict>();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StudentId { get; set; }
    public IEnumerable<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
    public bool HasAnyConflicts { get; set; }
}

public class ManageTimeSlotsViewModel
{
    public IEnumerable<TimeSlotListItemViewModel> TimeSlots { get; set; } = new List<TimeSlotListItemViewModel>();
}

public class ManageCreditsViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public IEnumerable<StudentPackage> StudentPackages { get; set; } = new List<StudentPackage>();
    public int TotalCreditsRemaining { get; set; }
}

public class BlockedDatesViewModel
{
    public IEnumerable<BlockedDate> BlockedDates { get; set; } = new List<BlockedDate>();
    public IEnumerable<BlockedDate> UpcomingBlockedDates { get; set; } = new List<BlockedDate>();
    public IEnumerable<BlockedDate> PastBlockedDates { get; set; } = new List<BlockedDate>();
}

public class CreateBlockedDateViewModel
{
    [Required]
    [Display(Name = "Start Date")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required]
    [Display(Name = "End Date")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Required]
    [Display(Name = "Reason")]
    [StringLength(200)]
    public string Reason { get; set; } = string.Empty;
}

public class RescheduleClassViewModel
{
    public int ClassId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public DateTime CurrentDateTime { get; set; }
    public int CurrentTimeSlotId { get; set; }
    public IEnumerable<TimeSlot> AvailableTimeSlots { get; set; } = new List<TimeSlot>();
    public DateTime NewDateTime { get; set; }
}

// Weekly Calendar DTOs for new booking view
public class WeeklyCalendarData
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public List<DaySchedule> Days { get; set; } = new();
    public List<TimeSlot> AllTimeSlots { get; set; } = new();  // Distinct time slots for row headers
}

public class DaySchedule
{
    public DateTime Date { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockedReason { get; set; }
    public List<TimeSlotAvailability> TimeSlots { get; set; } = new();
}

public class TimeSlotAvailability
{
    public int TimeSlotId { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsPastBookingCutoff { get; set; }  // Within 1 hour of now
    public bool SlotExistsForDay { get; set; }      // Does this slot operate on this day?
    public string? BookedByStudentName { get; set; } // For admin view (null for student view)
}

public class RecurringBookingResult
{
    public bool Success { get; set; }
    public List<ScheduledClass> BookedClasses { get; set; } = new();
    public List<DateTime> ConflictDates { get; set; } = new();
    public List<string> ConflictReasons { get; set; } = new();
    public int CreditsUsed { get; set; }
    public int CreditsRemaining { get; set; }
}

public class WeeklyBookingViewModel
{
    public WeeklyCalendarData WeeklyCalendar { get; set; } = null!;
    public int AvailableCredits { get; set; }
    public int MaxRecurringWeeks { get; set; }
    public decimal ClassPrice { get; set; }
}

public class TimeSlotListItemViewModel
{
    public TimeSlot TimeSlot { get; set; } = null!;
    public int ScheduledStudentCount { get; set; }
    public bool CanDelete { get; set; }
}

public class ScheduledClassWithConflict
{
    public ScheduledClass Class { get; set; } = null!;
    public bool HasBlockedDateConflict { get; set; }
}

/// <summary>
/// View model for the unified My Classes scheduling page.
/// </summary>
public class MyClassesViewModel
{
    /// <summary>
    /// Whether the student has an active subscription.
    /// </summary>
    public bool HasActiveSubscription { get; set; }

    /// <summary>
    /// Current subscription details (if any).
    /// </summary>
    public Subscription? CurrentSubscription { get; set; }

    /// <summary>
    /// Number of free class rewards (tickets) available.
    /// </summary>
    public int AvailableTickets { get; set; }

    /// <summary>
    /// Weekly calendar data for scheduling.
    /// </summary>
    public WeeklyCalendarData WeeklyCalendar { get; set; } = null!;

    /// <summary>
    /// Upcoming confirmed classes.
    /// </summary>
    public IEnumerable<ScheduledClass> UpcomingClasses { get; set; } = new List<ScheduledClass>();

    /// <summary>
    /// Classes that have been selected but not yet paid for.
    /// </summary>
    public IEnumerable<ScheduledClass> PendingCheckoutClasses { get; set; } = new List<ScheduledClass>();

    /// <summary>
    /// Checkout summary for non-subscribers.
    /// </summary>
    public SchedulingCheckoutSummary CheckoutSummary { get; set; } = new();

    /// <summary>
    /// For subscribers: remaining classes in their allocation.
    /// </summary>
    public int SubscriptionClassesRemaining { get; set; }

    /// <summary>
    /// Reserved classes from recurring schedules (future billing periods).
    /// </summary>
    public IEnumerable<ReservedClassViewModel> ReservedClasses { get; set; } = new List<ReservedClassViewModel>();

    /// <summary>
    /// Currently active tab: "lessons" or "calendar".
    /// </summary>
    public string ActiveTab { get; set; } = "lessons";
}

/// <summary>
/// Summary of pricing for class checkout.
/// </summary>
public class SchedulingCheckoutSummary
{
    /// <summary>
    /// Number of classes selected.
    /// </summary>
    public int ClassCount { get; set; }

    /// <summary>
    /// Price per class.
    /// </summary>
    public decimal PerClassPrice { get; set; }

    /// <summary>
    /// Subtotal before any discounts.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Number of free class rewards being applied.
    /// </summary>
    public int TicketsApplied { get; set; }

    /// <summary>
    /// Total discount from tickets.
    /// </summary>
    public decimal TicketDiscount { get; set; }

    /// <summary>
    /// Final amount due after discounts.
    /// </summary>
    public decimal TotalDue { get; set; }
}

/// <summary>
/// View model for reserved (future) classes that will be auto-scheduled.
/// Used in the Preply-style Lessons list view.
/// </summary>
public class ReservedClassViewModel
{
    /// <summary>
    /// The date/time for this reserved class.
    /// </summary>
    public DateTime ClassDateTime { get; set; }

    /// <summary>
    /// The time slot for this class.
    /// </summary>
    public TimeSlot TimeSlot { get; set; } = null!;

    /// <summary>
    /// Day of week for recurring schedules.
    /// </summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>
    /// Status message displayed to student (e.g., "Reserved - confirms on Feb 1").
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Whether this class is from a recurring schedule.
    /// </summary>
    public bool IsFromRecurringSchedule { get; set; }
}

/// <summary>
/// View model for the LessonsList ViewComponent.
/// </summary>
public class LessonsListViewModel
{
    /// <summary>
    /// Confirmed upcoming classes.
    /// </summary>
    public IList<ScheduledClass> UpcomingClasses { get; set; } = new List<ScheduledClass>();

    /// <summary>
    /// Reserved classes from recurring schedules (not yet confirmed).
    /// </summary>
    public IList<ReservedClassViewModel> ReservedClasses { get; set; } = new List<ReservedClassViewModel>();

    /// <summary>
    /// Current subscription (if any).
    /// </summary>
    public Subscription? Subscription { get; set; }

    /// <summary>
    /// Next billing date when reserved classes will be confirmed.
    /// </summary>
    public DateTime? NextBillingDate { get; set; }

    /// <summary>
    /// Whether the student has any upcoming or reserved classes.
    /// </summary>
    public bool HasAnyLessons => UpcomingClasses.Any() || ReservedClasses.Any();
}
