using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

public class ScheduleService : IScheduleService
{
    private readonly ApplicationDbContext _context;
    private readonly ITicketService _ticketService;

    public ScheduleService(ApplicationDbContext context, ITicketService ticketService)
    {
        _context = context;
        _ticketService = ticketService;
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

        return availableDates.Where(d => d > DateTime.UtcNow);
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

    // ============== NEW METHODS ==============

    public async Task<WeeklyCalendarData> GetWeeklyCalendarAsync(DateTime weekStart, string? studentId = null)
    {
        // Ensure weekStart is a Sunday
        weekStart = weekStart.Date.AddDays(-(int)weekStart.DayOfWeek);
        var weekEnd = weekStart.AddDays(6);

        var allTimeSlots = await _context.TimeSlots
            .Where(ts => ts.IsActive)
            .OrderBy(ts => ts.StartTime)
            .ToListAsync();

        var blockedDates = await _context.BlockedDates
            .Where(bd => bd.EndDate >= weekStart && bd.StartDate <= weekEnd)
            .ToListAsync();

        var bookedClasses = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Where(sc => sc.ClassDateTime >= weekStart &&
                         sc.ClassDateTime <= weekEnd.AddDays(1) &&
                         sc.Status != ClassStatus.Cancelled)
            .ToListAsync();

        var days = new List<DaySchedule>();

        for (var date = weekStart; date <= weekEnd; date = date.AddDays(1))
        {
            var blockedDate = blockedDates.FirstOrDefault(bd => date >= bd.StartDate && date <= bd.EndDate);
            var daySchedule = new DaySchedule
            {
                Date = date,
                IsBlocked = blockedDate != null,
                BlockedReason = blockedDate?.Reason,
                TimeSlots = new List<TimeSlotAvailability>()
            };

            foreach (var slot in allTimeSlots)
            {
                // Check if this slot operates on this day
                var slotExistsForDay = slot.IsRecurring
                    ? slot.DayOfWeek == date.DayOfWeek
                    : slot.SpecificDate?.Date == date;

                var classDateTime = date.Add(slot.StartTime);
                var bookedClass = bookedClasses.FirstOrDefault(bc =>
                    bc.TimeSlotId == slot.Id && bc.ClassDateTime.Date == date);

                var isPastCutoff = classDateTime <= DateTime.UtcNow.AddHours(1);

                daySchedule.TimeSlots.Add(new TimeSlotAvailability
                {
                    TimeSlotId = slot.Id,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    SlotExistsForDay = slotExistsForDay,
                    IsAvailable = slotExistsForDay && !daySchedule.IsBlocked && bookedClass == null && !isPastCutoff,
                    IsPastBookingCutoff = isPastCutoff,
                    BookedByStudentName = bookedClass?.Student?.FullName
                });
            }

            days.Add(daySchedule);
        }

        // Get distinct time slots by StartTime for row headers
        var distinctTimeSlots = allTimeSlots
            .GroupBy(ts => ts.StartTime)
            .Select(g => g.First())
            .OrderBy(ts => ts.StartTime)
            .ToList();

        return new WeeklyCalendarData
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Days = days,
            AllTimeSlots = distinctTimeSlots
        };
    }

    public async Task<RecurringBookingResult> BookRecurringClassesAsync(string studentId, int timeSlotId, DateTime startDate, int weekCount)
    {
        var result = new RecurringBookingResult();
        var timeSlot = await _context.TimeSlots.FindAsync(timeSlotId);

        if (timeSlot == null || !timeSlot.IsActive || !timeSlot.IsRecurring || !timeSlot.DayOfWeek.HasValue)
        {
            result.ConflictReasons.Add("Invalid or non-recurring time slot");
            return result;
        }

        // Get student's available credits
        var packages = await _context.StudentPackages
            .Where(sp => sp.StudentId == studentId && sp.ClassesRemaining > 0)
            .OrderBy(sp => sp.ExpirationDate)
            .ThenBy(sp => sp.PurchaseDate)
            .ToListAsync();

        var totalCredits = packages.Sum(p => p.ClassesRemaining);
        if (totalCredits < weekCount)
        {
            result.ConflictReasons.Add($"Not enough credits. Have {totalCredits}, need {weekCount}");
            return result;
        }

        // Calculate all target dates
        var targetDates = new List<DateTime>();
        var currentDate = startDate.Date;

        // Find first matching day of week
        while (currentDate.DayOfWeek != timeSlot.DayOfWeek.Value)
        {
            currentDate = currentDate.AddDays(1);
        }

        for (int i = 0; i < weekCount; i++)
        {
            targetDates.Add(currentDate.Add(timeSlot.StartTime));
            currentDate = currentDate.AddDays(7);
        }

        // Get blocked dates and existing bookings
        var blockedDates = await _context.BlockedDates.ToListAsync();
        var existingBookings = await _context.ScheduledClasses
            .Where(sc => sc.TimeSlotId == timeSlotId &&
                         sc.Status != ClassStatus.Cancelled &&
                         targetDates.Select(d => d.Date).Contains(sc.ClassDateTime.Date))
            .Select(sc => sc.ClassDateTime.Date)
            .ToListAsync();

        // Try to book each date
        foreach (var targetDateTime in targetDates)
        {
            // Check 1-hour cutoff
            if (!CanBookClass(targetDateTime))
            {
                result.ConflictDates.Add(targetDateTime);
                result.ConflictReasons.Add($"{targetDateTime:MMM d}: Too close to start time");
                continue;
            }

            // Check blocked dates
            if (blockedDates.Any(bd => targetDateTime.Date >= bd.StartDate && targetDateTime.Date <= bd.EndDate))
            {
                result.ConflictDates.Add(targetDateTime);
                result.ConflictReasons.Add($"{targetDateTime:MMM d}: Blocked date");
                continue;
            }

            // Check existing bookings
            if (existingBookings.Contains(targetDateTime.Date))
            {
                result.ConflictDates.Add(targetDateTime);
                result.ConflictReasons.Add($"{targetDateTime:MMM d}: Already booked");
                continue;
            }

            // Find package to use
            var packageToUse = packages.FirstOrDefault(p => p.ClassesRemaining > 0);
            if (packageToUse == null)
            {
                result.ConflictDates.Add(targetDateTime);
                result.ConflictReasons.Add($"{targetDateTime:MMM d}: No credits available");
                continue;
            }

            // Book the class
            var scheduledClass = new ScheduledClass
            {
                StudentId = studentId,
                TimeSlotId = timeSlotId,
                ClassDateTime = targetDateTime,
                Status = ClassStatus.Scheduled,
                PaymentStatus = PaymentStatus.PartOfPackage,
                StudentPackageId = packageToUse.Id
            };

            _context.ScheduledClasses.Add(scheduledClass);
            packageToUse.ClassesRemaining--;
            result.BookedClasses.Add(scheduledClass);
            result.CreditsUsed++;
        }

        await _context.SaveChangesAsync();

        result.Success = result.BookedClasses.Count > 0;
        result.CreditsRemaining = packages.Sum(p => p.ClassesRemaining);

        return result;
    }

    public bool CanBookClass(DateTime classDateTime)
    {
        return classDateTime > DateTime.UtcNow.AddHours(1);
    }

    public async Task<(bool canCancel, bool willForfeitCredit)> GetCancellationStatusAsync(int classId)
    {
        var scheduledClass = await _context.ScheduledClasses.FindAsync(classId);
        if (scheduledClass == null)
            return (false, false);

        var canCancel = scheduledClass.Status == ClassStatus.Scheduled;
        var hoursUntilClass = (scheduledClass.ClassDateTime - DateTime.UtcNow).TotalHours;
        var willForfeit = hoursUntilClass < 24;

        return (canCancel, willForfeit);
    }

    public async Task<bool> CancelClassWithForfeitAsync(int classId, string userId, bool isAdmin, bool acceptForfeit)
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

        var (_, willForfeit) = await GetCancellationStatusAsync(classId);

        // If within 24 hours and not accepting forfeit, deny
        if (willForfeit && !acceptForfeit && !isAdmin)
            return false;

        scheduledClass.Status = ClassStatus.Cancelled;

        // Handle ticket refunds (new system)
        if (scheduledClass.PaymentStatus == PaymentStatus.PaidWithTicket)
        {
            if (willForfeit && !isAdmin)
            {
                // Student cancelling late - forfeit the ticket
                scheduledClass.CreditForfeited = true;
            }
            else
            {
                // Admin cancelling or student cancelling with >24hr notice - restore ticket
                await _ticketService.RefundTicketAsync(classId);
            }
        }
        // Legacy: If class was paid for with a package
        else if (scheduledClass.PaymentStatus == PaymentStatus.PartOfPackage && scheduledClass.StudentPackageId.HasValue)
        {
            if (willForfeit && !isAdmin)
            {
                // Student cancelling late - forfeit the credit
                scheduledClass.CreditForfeited = true;
            }
            else
            {
                // Admin cancelling or student cancelling with >24hr notice - restore credit
                var package = await _context.StudentPackages.FindAsync(scheduledClass.StudentPackageId.Value);
                if (package != null)
                {
                    package.ClassesRemaining++;
                }
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HasScheduleConflictsAsync()
    {
        var today = DateTime.Today;
        var futureDate = today.AddDays(60);

        // Get all upcoming scheduled classes
        var scheduledClasses = await _context.ScheduledClasses
            .Where(sc => sc.ClassDateTime >= today &&
                         sc.ClassDateTime <= futureDate &&
                         sc.Status == ClassStatus.Scheduled)
            .Select(sc => sc.ClassDateTime.Date)
            .ToListAsync();

        if (!scheduledClasses.Any())
            return false;

        // Check if any of these dates fall within blocked periods
        var hasConflicts = await _context.BlockedDates
            .AnyAsync(bd => scheduledClasses.Any(d => d >= bd.StartDate && d <= bd.EndDate));

        return hasConflicts;
    }

    public async Task<IEnumerable<ScheduledClass>> GetClassesWithBlockedDateConflictsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        startDate ??= DateTime.Today;
        endDate ??= DateTime.Today.AddDays(60);

        var blockedDates = await _context.BlockedDates
            .Where(bd => bd.EndDate >= startDate && bd.StartDate <= endDate)
            .ToListAsync();

        if (!blockedDates.Any())
            return Enumerable.Empty<ScheduledClass>();

        var scheduledClasses = await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Include(sc => sc.TimeSlot)
            .Where(sc => sc.ClassDateTime >= startDate &&
                         sc.ClassDateTime <= endDate &&
                         sc.Status == ClassStatus.Scheduled)
            .ToListAsync();

        return scheduledClasses.Where(sc =>
            blockedDates.Any(bd => sc.ClassDateTime.Date >= bd.StartDate && sc.ClassDateTime.Date <= bd.EndDate));
    }

    public async Task<bool> HasOverlappingTimeSlotsAsync(DayOfWeek? day, TimeSpan start, TimeSpan end, int? excludeId = null)
    {
        if (!day.HasValue)
            return false;

        return await _context.TimeSlots
            .Where(ts => ts.IsActive && ts.IsRecurring && ts.DayOfWeek == day)
            .Where(ts => excludeId == null || ts.Id != excludeId)
            .AnyAsync(ts => start < ts.EndTime && end > ts.StartTime);
    }

    public async Task<int> GetScheduledStudentCountForTimeSlotAsync(int timeSlotId)
    {
        return await _context.ScheduledClasses
            .Where(sc => sc.TimeSlotId == timeSlotId &&
                         sc.Status == ClassStatus.Scheduled &&
                         sc.ClassDateTime >= DateTime.Today)
            .CountAsync();
    }

    public async Task<bool> CanDeleteTimeSlotAsync(int timeSlotId)
    {
        return !await _context.ScheduledClasses
            .AnyAsync(sc => sc.TimeSlotId == timeSlotId &&
                           sc.Status == ClassStatus.Scheduled &&
                           sc.ClassDateTime >= DateTime.Today);
    }

    public async Task<IEnumerable<ScheduledClass>> GetAffectedClassesForTimeSlotDeactivationAsync(int timeSlotId)
    {
        return await _context.ScheduledClasses
            .Include(sc => sc.Student)
            .Where(sc => sc.TimeSlotId == timeSlotId &&
                         sc.Status == ClassStatus.Scheduled &&
                         sc.ClassDateTime >= DateTime.Today)
            .OrderBy(sc => sc.ClassDateTime)
            .ToListAsync();
    }

    public async Task<bool> CompleteClassWithNotesAsync(int classId, string? notes)
    {
        var scheduledClass = await _context.ScheduledClasses.FindAsync(classId);
        if (scheduledClass == null || scheduledClass.Status != ClassStatus.Scheduled)
            return false;

        scheduledClass.Status = ClassStatus.Completed;
        scheduledClass.TeacherNotes = notes;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ReservedClassViewModel>> GetReservedClassesAsync(
        string studentId, DateTime fromDate, DateTime toDate)
    {
        // Get active subscription with recurring schedules
        var subscription = await _context.Subscriptions
            .Include(s => s.RecurringSchedules)
            .ThenInclude(rs => rs.TimeSlot)
            .Include(s => s.Tier)
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == SubscriptionStatus.Active);

        if (subscription == null || !subscription.RecurringSchedules.Any())
            return Enumerable.Empty<ReservedClassViewModel>();

        var reserved = new List<ReservedClassViewModel>();
        var nextBillingDate = subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);

        // Get existing scheduled classes to avoid duplicates
        var existingClassDates = await _context.ScheduledClasses
            .Where(sc => sc.StudentId == studentId
                && sc.ClassDateTime >= fromDate
                && sc.ClassDateTime <= toDate
                && sc.Status != ClassStatus.Cancelled)
            .Select(sc => sc.ClassDateTime)
            .ToListAsync();

        // Get blocked dates to exclude
        var blockedDates = await _context.BlockedDates
            .Where(bd => bd.EndDate >= fromDate && bd.StartDate <= toDate)
            .ToListAsync();

        // Generate reserved slots from recurring schedules
        foreach (var schedule in subscription.RecurringSchedules.Where(rs => rs.IsActive))
        {
            var date = fromDate.Date;
            while (date <= toDate.Date)
            {
                // Only show reserved classes after the current billing period end
                if (date.DayOfWeek == schedule.DayOfWeek && date >= nextBillingDate.Date)
                {
                    var classDateTime = date.Add(schedule.Time);

                    // Skip if class already exists
                    if (existingClassDates.Any(ec => ec.Date == classDateTime.Date && ec.TimeOfDay == classDateTime.TimeOfDay))
                    {
                        date = date.AddDays(1);
                        continue;
                    }

                    // Skip if date is blocked
                    if (blockedDates.Any(bd => date >= bd.StartDate && date <= bd.EndDate))
                    {
                        date = date.AddDays(1);
                        continue;
                    }

                    reserved.Add(new ReservedClassViewModel
                    {
                        ClassDateTime = classDateTime,
                        TimeSlot = schedule.TimeSlot,
                        DayOfWeek = schedule.DayOfWeek,
                        IsFromRecurringSchedule = true,
                        StatusMessage = $"Reserved - will be confirmed when your subscription renews on {nextBillingDate:MMM d}"
                    });
                }
                date = date.AddDays(1);
            }
        }

        return reserved.OrderBy(r => r.ClassDateTime);
    }
}
