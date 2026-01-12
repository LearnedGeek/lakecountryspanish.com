using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service for processing and sending scheduled notifications.
/// </summary>
public class NotificationScheduler : INotificationScheduler
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationScheduler> _logger;

    // Point milestones that trigger notifications
    private static readonly int[] PointMilestones = { 100, 250, 500, 1000, 2500, 5000, 10000 };

    public NotificationScheduler(
        ApplicationDbContext context,
        IEmailService emailService,
        ILogger<NotificationScheduler> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ProcessClassRemindersAsync()
    {
        var now = DateTime.UtcNow;
        var twentyFourHoursFromNow = now.AddHours(24);
        var oneHourFromNow = now.AddHours(1);

        // Get classes that need 24-hour reminders
        var classesFor24HourReminder = await _context.ScheduledClasses
            .Include(c => c.Student)
            .Where(c => c.Status == ClassStatus.Scheduled &&
                       c.ClassDateTime > now &&
                       c.ClassDateTime <= twentyFourHoursFromNow &&
                       c.ClassDateTime > now.AddHours(23)) // Only classes 23-24 hours away
            .ToListAsync();

        foreach (var scheduledClass in classesFor24HourReminder)
        {
            if (await HasNotificationBeenSent(NotificationType.ClassReminder24Hours, scheduledClass.Id, scheduledClass.StudentId))
                continue;

            try
            {
                await _emailService.SendClassReminderAsync(
                    scheduledClass.Student.Email ?? "",
                    scheduledClass.Student.FirstName,
                    scheduledClass.ClassDateTime,
                    scheduledClass.Student.ClassroomUrl,
                    24);

                await LogNotification(
                    scheduledClass.StudentId,
                    scheduledClass.Student.Email ?? "",
                    NotificationType.ClassReminder24Hours,
                    "Class Reminder - 24 Hours",
                    true,
                    null,
                    scheduledClass.Id);

                _logger.LogInformation("Sent 24-hour reminder for class {ClassId} to {Email}",
                    scheduledClass.Id, scheduledClass.Student.Email);
            }
            catch (Exception ex)
            {
                await LogNotification(
                    scheduledClass.StudentId,
                    scheduledClass.Student.Email ?? "",
                    NotificationType.ClassReminder24Hours,
                    "Class Reminder - 24 Hours",
                    false,
                    ex.Message,
                    scheduledClass.Id);

                _logger.LogError(ex, "Failed to send 24-hour reminder for class {ClassId}", scheduledClass.Id);
            }
        }

        // Get classes that need 1-hour reminders
        var classesFor1HourReminder = await _context.ScheduledClasses
            .Include(c => c.Student)
            .Where(c => c.Status == ClassStatus.Scheduled &&
                       c.ClassDateTime > now &&
                       c.ClassDateTime <= oneHourFromNow)
            .ToListAsync();

        foreach (var scheduledClass in classesFor1HourReminder)
        {
            if (await HasNotificationBeenSent(NotificationType.ClassReminder1Hour, scheduledClass.Id, scheduledClass.StudentId))
                continue;

            try
            {
                await _emailService.SendClassReminderAsync(
                    scheduledClass.Student.Email ?? "",
                    scheduledClass.Student.FirstName,
                    scheduledClass.ClassDateTime,
                    scheduledClass.Student.ClassroomUrl,
                    1);

                await LogNotification(
                    scheduledClass.StudentId,
                    scheduledClass.Student.Email ?? "",
                    NotificationType.ClassReminder1Hour,
                    "Class Reminder - 1 Hour",
                    true,
                    null,
                    scheduledClass.Id);

                _logger.LogInformation("Sent 1-hour reminder for class {ClassId} to {Email}",
                    scheduledClass.Id, scheduledClass.Student.Email);
            }
            catch (Exception ex)
            {
                await LogNotification(
                    scheduledClass.StudentId,
                    scheduledClass.Student.Email ?? "",
                    NotificationType.ClassReminder1Hour,
                    "Class Reminder - 1 Hour",
                    false,
                    ex.Message,
                    scheduledClass.Id);

                _logger.LogError(ex, "Failed to send 1-hour reminder for class {ClassId}", scheduledClass.Id);
            }
        }
    }

    public async Task ProcessPointMilestonesAsync()
    {
        // Get students with their total points
        var studentPoints = await _context.PointTransactions
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, TotalPoints = g.Sum(p => p.FinalPoints) })
            .ToListAsync();

        foreach (var sp in studentPoints)
        {
            // Find the highest milestone they've reached
            var reachedMilestones = PointMilestones.Where(m => sp.TotalPoints >= m).ToList();

            foreach (var milestone in reachedMilestones)
            {
                // Check if we've already notified about this milestone
                if (await HasMilestoneNotificationBeenSent(sp.StudentId, milestone))
                    continue;

                // Get student info
                var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == sp.StudentId);
                if (student == null) continue;

                // Determine token reward (1 token per 1000 points milestone)
                var tokensEarned = milestone >= 1000 ? milestone / 1000 : 0;

                try
                {
                    await _emailService.SendPointMilestoneAsync(
                        student.Email ?? "",
                        student.FirstName,
                        sp.TotalPoints,
                        milestone,
                        tokensEarned);

                    // Log with milestone as related entity ID (multiplied to make unique)
                    await LogNotification(
                        sp.StudentId,
                        student.Email ?? "",
                        NotificationType.PointMilestone,
                        $"Point Milestone - {milestone:N0} Points",
                        true,
                        null,
                        milestone);

                    _logger.LogInformation("Sent {Milestone} point milestone notification to {Email}",
                        milestone, student.Email);
                }
                catch (Exception ex)
                {
                    await LogNotification(
                        sp.StudentId,
                        student.Email ?? "",
                        NotificationType.PointMilestone,
                        $"Point Milestone - {milestone:N0} Points",
                        false,
                        ex.Message,
                        milestone);

                    _logger.LogError(ex, "Failed to send milestone notification to {Email}", student.Email);
                }
            }
        }
    }

    public async Task SendWeeklyProgressReportsAsync()
    {
        var now = DateTime.UtcNow;
        var oneWeekAgo = now.AddDays(-7);

        // Get all active students
        var activeStudents = await _context.Users
            .Where(u => u.IsActive && u.PointTransactions.Any())
            .ToListAsync();

        foreach (var student in activeStudents)
        {
            // Check if we already sent this week's report
            var lastReport = await _context.NotificationLogs
                .Where(n => n.RecipientId == student.Id &&
                           n.Type == NotificationType.WeeklyProgressReport &&
                           n.SentAt >= oneWeekAgo)
                .FirstOrDefaultAsync();

            if (lastReport != null) continue;

            // Gather weekly progress data
            var progressData = await GatherWeeklyProgressData(student.Id, oneWeekAgo, now);

            try
            {
                await _emailService.SendWeeklyProgressReportAsync(
                    student.Email ?? "",
                    student.FirstName,
                    progressData);

                await LogNotification(
                    student.Id,
                    student.Email ?? "",
                    NotificationType.WeeklyProgressReport,
                    "Weekly Progress Report",
                    true,
                    null,
                    null);

                _logger.LogInformation("Sent weekly progress report to {Email}", student.Email);
            }
            catch (Exception ex)
            {
                await LogNotification(
                    student.Id,
                    student.Email ?? "",
                    NotificationType.WeeklyProgressReport,
                    "Weekly Progress Report",
                    false,
                    ex.Message,
                    null);

                _logger.LogError(ex, "Failed to send weekly progress report to {Email}", student.Email);
            }
        }
    }

    private async Task<WeeklyProgressData> GatherWeeklyProgressData(string studentId, DateTime startDate, DateTime endDate)
    {
        // Get student name
        var student = await _context.Users.FirstAsync(u => u.Id == studentId);

        // Classes attended this week
        var classesAttended = await _context.ScheduledClasses
            .Where(c => c.StudentId == studentId &&
                       c.Status == ClassStatus.Completed &&
                       c.ClassDateTime >= startDate &&
                       c.ClassDateTime < endDate)
            .CountAsync();

        // Assignments completed this week
        var assignmentsCompleted = await _context.AssignmentSubmissions
            .Where(s => s.StudentId == studentId &&
                       s.SubmittedAt >= startDate &&
                       s.SubmittedAt < endDate)
            .CountAsync();

        // Points earned this week
        var pointsEarned = await _context.PointTransactions
            .Where(p => p.StudentId == studentId &&
                       p.CreatedAt >= startDate &&
                       p.CreatedAt < endDate &&
                       p.FinalPoints > 0)
            .SumAsync(p => (int?)p.FinalPoints) ?? 0;

        // Total points
        var totalPoints = await _context.PointTransactions
            .Where(p => p.StudentId == studentId)
            .SumAsync(p => (int?)p.FinalPoints) ?? 0;

        // Current streak
        var streak = await _context.StudentStreaks
            .Where(s => s.StudentId == studentId)
            .Select(s => s.CurrentStreak)
            .FirstOrDefaultAsync();

        // Badges earned this week
        var badgesEarned = await _context.StudentBadges
            .Where(b => b.StudentId == studentId &&
                       b.EarnedAt >= startDate &&
                       b.EarnedAt < endDate)
            .Include(b => b.Badge)
            .Select(b => b.Badge.Name)
            .ToListAsync();

        // Calculate next milestone
        var nextMilestone = PointMilestones.FirstOrDefault(m => m > totalPoints);
        var pointsToNext = nextMilestone > 0 ? nextMilestone - totalPoints : 0;

        return new WeeklyProgressData
        {
            StudentName = student.FirstName,
            ClassesAttended = classesAttended,
            AssignmentsCompleted = assignmentsCompleted,
            PointsEarned = pointsEarned,
            CurrentStreak = streak,
            TotalPoints = totalPoints,
            BadgesEarned = badgesEarned,
            NextMilestone = nextMilestone > 0 ? $"{nextMilestone:N0} points" : null,
            PointsToNextMilestone = pointsToNext
        };
    }

    private async Task<bool> HasNotificationBeenSent(NotificationType type, int relatedEntityId, string recipientId)
    {
        return await _context.NotificationLogs
            .AnyAsync(n => n.Type == type &&
                          n.RelatedEntityId == relatedEntityId &&
                          n.RecipientId == recipientId &&
                          n.WasSent);
    }

    private async Task<bool> HasMilestoneNotificationBeenSent(string studentId, int milestone)
    {
        return await _context.NotificationLogs
            .AnyAsync(n => n.Type == NotificationType.PointMilestone &&
                          n.RelatedEntityId == milestone &&
                          n.RecipientId == studentId &&
                          n.WasSent);
    }

    private async Task LogNotification(
        string recipientId,
        string recipientEmail,
        NotificationType type,
        string subject,
        bool wasSent,
        string? errorMessage,
        int? relatedEntityId)
    {
        var log = new NotificationLog
        {
            RecipientId = recipientId,
            RecipientEmail = recipientEmail,
            Type = type,
            Subject = subject,
            WasSent = wasSent,
            ErrorMessage = errorMessage,
            RelatedEntityId = relatedEntityId,
            SentAt = DateTime.UtcNow
        };

        _context.NotificationLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
