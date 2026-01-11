using SpanishScheduler.Web.Models.Entities;

namespace SpanishScheduler.Web.Services;

public interface IScheduleService
{
    Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync();
    Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int timeSlotId, DateTime startDate, DateTime endDate);
    Task<ScheduledClass?> BookClassAsync(string studentId, int timeSlotId, DateTime classDateTime);
    Task<IEnumerable<ScheduledClass>> GetStudentClassesAsync(string studentId);
    Task<IEnumerable<ScheduledClass>> GetAllClassesAsync(DateTime? startDate = null, DateTime? endDate = null, string? studentId = null);
    Task<bool> CancelClassAsync(int classId, string userId, bool isAdmin);
    Task<bool> CompleteClassAsync(int classId);
    Task<bool> IsTimeSlotAvailableAsync(int timeSlotId, DateTime classDateTime);
}
