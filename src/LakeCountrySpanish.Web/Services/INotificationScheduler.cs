namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service for processing and sending scheduled notifications.
/// </summary>
public interface INotificationScheduler
{
    /// <summary>
    /// Process and send class reminder emails for upcoming classes.
    /// </summary>
    Task ProcessClassRemindersAsync();

    /// <summary>
    /// Process and send point milestone notifications.
    /// </summary>
    Task ProcessPointMilestonesAsync();

    /// <summary>
    /// Send weekly progress reports to all active students.
    /// </summary>
    Task SendWeeklyProgressReportsAsync();
}
