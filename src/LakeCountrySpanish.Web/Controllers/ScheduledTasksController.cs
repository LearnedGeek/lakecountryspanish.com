using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// API endpoints for scheduled tasks. These are called by the hosting provider's
/// task scheduler (SmarterASP) to run background jobs.
///
/// Authentication: All endpoints (except /health) require a secret key.
/// The key can be provided via:
///   1. X-Task-Key header (preferred - not logged)
///   2. ?key= query parameter (for SmarterASP compatibility - may appear in logs)
/// </summary>
[Route("api/tasks")]
[ApiController]
public class ScheduledTasksController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IClassSchedulingService _classSchedulingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScheduledTasksController> _logger;

    public ScheduledTasksController(
        ApplicationDbContext context,
        IEmailService emailService,
        IClassSchedulingService classSchedulingService,
        IConfiguration configuration,
        ILogger<ScheduledTasksController> logger)
    {
        _context = context;
        _emailService = emailService;
        _classSchedulingService = classSchedulingService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Validates the task scheduler secret key.
    /// Accepts key from either:
    /// 1. X-Task-Key header (preferred, more secure)
    /// 2. Query parameter (for schedulers like SmarterASP that only support URLs)
    /// </summary>
    private bool ValidateSecretKey(string? queryKey)
    {
        var configuredKey = _configuration["ScheduledTasks:SecretKey"];

        // If no key configured, reject all requests (secure by default)
        if (string.IsNullOrEmpty(configuredKey))
        {
            _logger.LogWarning("Scheduled task endpoint called but no secret key configured");
            return false;
        }

        // First check header (preferred method - doesn't appear in logs)
        var headerKey = Request.Headers["X-Task-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerKey))
        {
            return string.Equals(headerKey, configuredKey, StringComparison.Ordinal);
        }

        // Fall back to query parameter (for SmarterASP compatibility)
        // Note: Query params may appear in server logs - use header when possible
        if (!string.IsNullOrEmpty(queryKey))
        {
            _logger.LogDebug("Task authentication via query parameter (consider using X-Task-Key header instead)");
            return string.Equals(queryKey, configuredKey, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// Send class reminder emails to students with classes coming up.
    /// Called every 30 minutes by the task scheduler.
    ///
    /// Sends reminders at:
    /// - 24 hours before class
    /// - 1 hour before class
    /// </summary>
    [HttpGet("send-class-reminders")]
    public async Task<IActionResult> SendClassReminders([FromQuery] string? key)
    {
        if (!ValidateSecretKey(key))
        {
            _logger.LogWarning("Unauthorized class reminders task attempt");
            return Unauthorized(new { error = "Invalid or missing secret key" });
        }

        _logger.LogInformation("Starting scheduled task: SendClassReminders");

        var now = DateTime.UtcNow;
        var remindersSent = 0;
        var errors = 0;

        try
        {
            // Find classes that need 24-hour reminders
            // Classes between 23.5 and 24.5 hours from now that haven't received 24hr reminder
            var twentyFourHourWindow = await _context.ScheduledClasses
                .Include(sc => sc.Student)
                .Include(sc => sc.TimeSlot)
                .Where(sc => sc.Status == ClassStatus.Scheduled &&
                             sc.ClassDateTime > now.AddHours(23.5) &&
                             sc.ClassDateTime <= now.AddHours(24.5) &&
                             !sc.Reminder24HrSent)
                .ToListAsync();

            foreach (var scheduledClass in twentyFourHourWindow)
            {
                if (scheduledClass.Student?.Email == null) continue;

                try
                {
                    await _emailService.SendClassReminderAsync(
                        scheduledClass.Student.Email,
                        scheduledClass.Student.FirstName ?? scheduledClass.Student.Email,
                        scheduledClass.ClassDateTime,
                        scheduledClass.Student.ClassroomUrl,
                        24);

                    scheduledClass.Reminder24HrSent = true;
                    remindersSent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send 24hr reminder for class {ClassId}", scheduledClass.Id);
                    errors++;
                }
            }

            // Find classes that need 1-hour reminders
            // Classes between 0.5 and 1.5 hours from now that haven't received 1hr reminder
            var oneHourWindow = await _context.ScheduledClasses
                .Include(sc => sc.Student)
                .Include(sc => sc.TimeSlot)
                .Where(sc => sc.Status == ClassStatus.Scheduled &&
                             sc.ClassDateTime > now.AddMinutes(30) &&
                             sc.ClassDateTime <= now.AddMinutes(90) &&
                             !sc.Reminder1HrSent)
                .ToListAsync();

            foreach (var scheduledClass in oneHourWindow)
            {
                if (scheduledClass.Student?.Email == null) continue;

                try
                {
                    await _emailService.SendClassReminderAsync(
                        scheduledClass.Student.Email,
                        scheduledClass.Student.FirstName ?? scheduledClass.Student.Email,
                        scheduledClass.ClassDateTime,
                        scheduledClass.Student.ClassroomUrl,
                        1);

                    scheduledClass.Reminder1HrSent = true;
                    remindersSent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send 1hr reminder for class {ClassId}", scheduledClass.Id);
                    errors++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("SendClassReminders completed: {RemindersSent} sent, {Errors} errors",
                remindersSent, errors);

            return Ok(new {
                success = true,
                remindersSent,
                errors,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendClassReminders task failed");
            return StatusCode(500, new { error = "Task failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Cleanup stale pending checkout classes (abandoned carts).
    /// Called every 30 minutes by the task scheduler.
    /// Removes classes that have been in pending checkout for more than 30 minutes.
    /// </summary>
    [HttpGet("cleanup-stale-checkouts")]
    public async Task<IActionResult> CleanupStaleCheckouts([FromQuery] string? key)
    {
        if (!ValidateSecretKey(key))
        {
            _logger.LogWarning("Unauthorized cleanup task attempt");
            return Unauthorized(new { error = "Invalid or missing secret key" });
        }

        _logger.LogInformation("Starting scheduled task: CleanupStaleCheckouts");

        try
        {
            var cleanedUp = await _classSchedulingService.CleanupStalePendingClassesAsync(30);

            _logger.LogInformation("CleanupStaleCheckouts completed: {Count} stale classes removed", cleanedUp);

            return Ok(new {
                success = true,
                classesRemoved = cleanedUp,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupStaleCheckouts task failed");
            return StatusCode(500, new { error = "Task failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Report on expired tickets (for monitoring).
    /// Tickets are considered expired based on ExpiresAt date.
    /// Called daily by the task scheduler for reporting purposes.
    /// </summary>
    [HttpGet("check-expired-tickets")]
    public async Task<IActionResult> CheckExpiredTickets([FromQuery] string? key)
    {
        if (!ValidateSecretKey(key))
        {
            _logger.LogWarning("Unauthorized check expired tickets task attempt");
            return Unauthorized(new { error = "Invalid or missing secret key" });
        }

        _logger.LogInformation("Starting scheduled task: CheckExpiredTickets");

        try
        {
            var now = DateTime.UtcNow;

            // Count expired unused tickets (ExpiresAt < now and not used)
            var expiredCount = await _context.Tickets
                .CountAsync(t => !t.IsUsed &&
                            t.ExpiresAt.HasValue &&
                            t.ExpiresAt.Value < now);

            // Count active (non-expired, unused) tickets
            var activeCount = await _context.Tickets
                .CountAsync(t => !t.IsUsed &&
                            (!t.ExpiresAt.HasValue || t.ExpiresAt.Value >= now));

            _logger.LogInformation("CheckExpiredTickets completed: {ExpiredCount} expired, {ActiveCount} active",
                expiredCount, activeCount);

            return Ok(new {
                success = true,
                expiredTickets = expiredCount,
                activeTickets = activeCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckExpiredTickets task failed");
            return StatusCode(500, new { error = "Task failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Health check endpoint - verifies the scheduled tasks system is working.
    /// Can be called without authentication for monitoring purposes.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            endpoints = new[] {
                "send-class-reminders",
                "cleanup-stale-checkouts",
                "check-expired-tickets"
            }
        });
    }
}
