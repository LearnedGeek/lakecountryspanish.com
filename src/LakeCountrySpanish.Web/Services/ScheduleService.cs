using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services;

public class ScheduleService : IScheduleService
{
    private readonly ApplicationDbContext _context;

    public ScheduleService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync()
    {
        return await _context.TimeSlots
            .Where(ts => ts.IsActive)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int timeSlotId, DateTime startDate, DateTime endDate)
    {
        var timeSlot = await _context.TimeSlots.FindAsync(timeSlotId);
        if (timeSlot == null || !timeSlot.IsActive)
            return Enumerable.Empty<DateTime>();

        var availableDates = new List<DateTime>();
        var bookedDates = await _context.ScheduledClasses
            .Where(sc => sc.TimeSlotId == timeSlotId &&
                         sc.ClassDateTime >= startDate &&
                         sc.ClassDateTime <= endDate &&
                         sc.Status != ClassStatus.Cancelled)
            .Select(sc => sc.ClassDateTime.Date)
            .ToListAsync();

        // Get blocked date periods
        var blockedPeriods = await _context.BlockedDates
            .Where(bd => bd.EndDate >= startDate && bd.StartDate <= endDate)
            .ToListAsync();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            // Skip blocked dates (vacation, holidays, etc.)
            if (blockedPeriods.Any(bp => date >= bp.StartDate && date <= bp.EndDate))
                continue;

            // For recurring slots, check if day of week matches
            if (timeSlot.IsRecurring && timeSlot.DayOfWeek.HasValue)
            {
                if (date.DayOfWeek == timeSlot.DayOfWeek.Value && !bookedDates.Contains(date))
                {
                    availableDates.Add(date.Add(timeSlot.StartTime));
                }
            }
            // For specific date slots
            else if (!timeSlot.IsRecurring && timeSlot.SpecificDate.HasValue)
            {
                if (date == timeSlot.SpecificDate.Value.Date && !bookedDates.Contains(date))
                {
                    availableDates.Add(date.Add(timeSlot.StartTime));
                }
            }
        }

        return availableDates.Where(d => d > DateTime.Now);
    }

    public async Task<ScheduledClass?> BookClassAsync(string studentId, int timeSlotId, DateTime classDateTime)
    {
        // Verify the time slot is available
        if (!await IsTimeSlotAvailableAsync(timeSlotId, classDateTime))
            return null;

        var scheduledClass = new ScheduledClass
        {
            StudentId = studentId,
            TimeSlotId = timeSlotId,
            ClassDateTime = classDateTime,
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid
        };

        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        return scheduledClass;
    }

    public async Task<IEnumerable<ScheduledClass>> GetStudentClassesAsync(string studentId)
    {
        return await _context.ScheduledClasses
            .Include(sc => sc.TimeSlot)
            .Include(sc => sc.Payment)
            .Where(sc => sc.StudentId == studentId)
            .OrderByDescending(sc => sc.ClassDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<ScheduledClass>> GetAllClassesAsync(DateTime? startDate = null, DateTime? endDate = null, string? studentId = null)
    {
        var query = _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Include(sc => sc.TimeSlot)
            .Include(sc => sc.Payment)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(sc => sc.ClassDateTime >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(sc => sc.ClassDateTime <= endDate.Value);

        if (!string.IsNullOrEmpty(studentId))
            query = query.Where(sc => sc.StudentId == studentId);

        return await query.OrderBy(sc => sc.ClassDateTime).ToListAsync();
    }

    public async Task<bool> CancelClassAsync(int classId, string userId, bool isAdmin)
    {
        var scheduledClass = await _context.ScheduledClasses
            .FirstOrDefaultAsync(sc => sc.Id == classId);

        if (scheduledClass == null)
            return false;

        // Only allow cancellation if admin or the class belongs to the user
        if (!isAdmin && scheduledClass.StudentId != userId)
            return false;

        // Only allow cancellation of scheduled classes
        if (scheduledClass.Status != ClassStatus.Scheduled)
            return false;

        scheduledClass.Status = ClassStatus.Cancelled;

        // If class was paid for with a package, restore the class credit
        if (scheduledClass.PaymentStatus == PaymentStatus.PartOfPackage && scheduledClass.StudentPackageId.HasValue)
        {
            var package = await _context.StudentPackages.FindAsync(scheduledClass.StudentPackageId.Value);
            if (package != null)
            {
                package.ClassesRemaining++;
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CompleteClassAsync(int classId)
    {
        var scheduledClass = await _context.ScheduledClasses.FindAsync(classId);
        if (scheduledClass == null || scheduledClass.Status != ClassStatus.Scheduled)
            return false;

        scheduledClass.Status = ClassStatus.Completed;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsTimeSlotAvailableAsync(int timeSlotId, DateTime classDateTime)
    {
        var timeSlot = await _context.TimeSlots.FindAsync(timeSlotId);
        if (timeSlot == null || !timeSlot.IsActive)
            return false;

        // Check if date is blocked (vacation, holiday, etc.)
        var isBlocked = await _context.BlockedDates
            .AnyAsync(bd => classDateTime.Date >= bd.StartDate && classDateTime.Date <= bd.EndDate);
        if (isBlocked)
            return false;

        // Check if there's already a class booked at this time
        var existingClass = await _context.ScheduledClasses
            .AnyAsync(sc => sc.TimeSlotId == timeSlotId &&
                           sc.ClassDateTime.Date == classDateTime.Date &&
                           sc.Status != ClassStatus.Cancelled);

        return !existingClass;
    }
}
