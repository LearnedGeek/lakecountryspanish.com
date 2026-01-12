namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Type of notification sent to users.
/// </summary>
public enum NotificationType
{
    ClassReminder24Hours,
    ClassReminder1Hour,
    PointMilestone,
    BadgeEarned,
    PaymentConfirmation,
    SubscriptionRenewal,
    AssignmentAssigned,
    WeeklyProgressReport
}

/// <summary>
/// Log of notifications sent to users for tracking and preventing duplicates.
/// </summary>
public class NotificationLog
{
    public int Id { get; set; }

    /// <summary>
    /// User ID of the recipient.
    /// </summary>
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// Email address the notification was sent to.
    /// </summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// Type of notification.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Email subject line.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Whether the notification was successfully sent.
    /// </summary>
    public bool WasSent { get; set; }

    /// <summary>
    /// Error message if sending failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// ID of the related entity (ScheduledClassId, PaymentId, etc.)
    /// </summary>
    public int? RelatedEntityId { get; set; }

    /// <summary>
    /// When the notification was sent (or attempted).
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser Recipient { get; set; } = null!;
}
