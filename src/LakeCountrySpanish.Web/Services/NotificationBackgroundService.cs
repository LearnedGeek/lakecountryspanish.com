namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Background service that runs notification checks on a schedule.
/// </summary>
public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;

    // Run every 15 minutes
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public NotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing notifications");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Notification Background Service stopped");
    }

    private async Task ProcessNotificationsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<INotificationScheduler>();

        _logger.LogDebug("Processing class reminders...");
        await scheduler.ProcessClassRemindersAsync();

        _logger.LogDebug("Processing point milestones...");
        await scheduler.ProcessPointMilestonesAsync();

        // Weekly reports only on Monday mornings
        var now = DateTime.UtcNow;
        if (now.DayOfWeek == DayOfWeek.Monday && now.Hour >= 9 && now.Hour < 10)
        {
            _logger.LogDebug("Processing weekly progress reports...");
            await scheduler.SendWeeklyProgressReportsAsync();
        }

        _logger.LogDebug("Notification processing complete");
    }
}
