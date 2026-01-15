using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

public interface IScheduleService
{
    // Existing methods
    Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync();
    Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int timeSlotId, DateTime startDate, DateTime endDate);
    Task<ScheduledClass?> BookClassAsync(string studentId, int timeSlotId, DateTime classDateTime);
    Task<IEnumerable<ScheduledClass>> GetStudentClassesAsync(string studentId);
    Task<IEnumerable<ScheduledClass>> GetAllClassesAsync(DateTime? startDate = null, DateTime? endDate = null, string? studentId = null);
    Task<bool> CancelClassAsync(int classId, string userId, bool isAdmin);
    Task<bool> CompleteClassAsync(int classId);
    Task<bool> IsTimeSlotAvailableAsync(int timeSlotId, DateTime classDateTime);

    // Weekly calendar for booking view
    Task<WeeklyCalendarData> GetWeeklyCalendarAsync(DateTime weekStart, string? studentId = null);

    // Recurring booking (returns partial success with conflicts)
    Task<RecurringBookingResult> BookRecurringClassesAsync(string studentId, int timeSlotId, DateTime startDate, int weekCount);

    // Time restrictions
    bool CanBookClass(DateTime classDateTime);  // Must be >1 hour from now
    Task<(bool canCancel, bool willForfeitCredit)> GetCancellationStatusAsync(int classId);

    // Enhanced cancellation with forfeit tracking
    Task<bool> CancelClassWithForfeitAsync(int classId, string userId, bool isAdmin, bool acceptForfeit);

    // Conflict detection
    Task<bool> HasScheduleConflictsAsync();  // Any blocked dates affecting scheduled classes
    Task<IEnumerable<ScheduledClass>> GetClassesWithBlockedDateConflictsAsync(DateTime? startDate = null, DateTime? endDate = null);

    // TimeSlot validation
    Task<bool> HasOverlappingTimeSlotsAsync(DayOfWeek? day, TimeSpan start, TimeSpan end, int? excludeId = null);
    Task<int> GetScheduledStudentCountForTimeSlotAsync(int timeSlotId);
    Task<bool> CanDeleteTimeSlotAsync(int timeSlotId);
    Task<IEnumerable<ScheduledClass>> GetAffectedClassesForTimeSlotDeactivationAsync(int timeSlotId);

    // Enhanced completion with notes
    Task<bool> CompleteClassWithNotesAsync(int classId, string? notes);

    // Reserved classes (for Preply-style Lessons view)
    /// <summary>
    /// Gets reserved classes from recurring schedules for the specified date range.
    /// These are future classes that will be auto-scheduled when the subscription renews.
    /// </summary>
    Task<IEnumerable<ReservedClassViewModel>> GetReservedClassesAsync(string studentId, DateTime fromDate, DateTime toDate);
}
