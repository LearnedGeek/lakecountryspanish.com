using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service for managing the class scheduling checkout flow.
/// Handles adding classes to a pending checkout cart, applying ticket discounts,
/// and creating Stripe checkout sessions for multiple classes.
/// </summary>
public interface IClassSchedulingService
{
    /// <summary>
    /// Adds a class to the student's pending checkout cart.
    /// Creates a ScheduledClass with IsPendingCheckout = true.
    /// </summary>
    Task<ScheduledClass?> AddClassToCheckoutAsync(string studentId, int timeSlotId, DateTime classDateTime);

    /// <summary>
    /// Removes a class from the pending checkout cart.
    /// Only works for classes that are still pending checkout.
    /// </summary>
    Task<bool> RemoveFromCheckoutAsync(int classId, string studentId);

    /// <summary>
    /// Gets all pending checkout classes for a student.
    /// </summary>
    Task<IEnumerable<ScheduledClass>> GetPendingCheckoutAsync(string studentId);

    /// <summary>
    /// Clears all pending checkout classes for a student (e.g., on timeout or explicit clear).
    /// </summary>
    Task<int> ClearPendingCheckoutAsync(string studentId);

    /// <summary>
    /// Gets a checkout summary with pricing, including optional ticket discounts.
    /// </summary>
    Task<SchedulingCheckoutSummary> GetCheckoutSummaryAsync(string studentId, int ticketsToApply = 0);

    /// <summary>
    /// Creates a Stripe checkout session for all pending classes.
    /// Applies ticket discounts if requested.
    /// </summary>
    Task<string?> CreateCheckoutSessionAsync(string studentId, int ticketsToApply, string successUrl, string cancelUrl);

    /// <summary>
    /// Called when Stripe checkout is completed.
    /// Marks classes as confirmed and consumes tickets.
    /// </summary>
    Task<bool> CompleteCheckoutAsync(string stripeSessionId);

    /// <summary>
    /// For subscribers: immediately confirms a class using subscription allocation.
    /// No checkout needed.
    /// </summary>
    Task<ScheduledClass?> ScheduleWithSubscriptionAsync(string studentId, int timeSlotId, DateTime classDateTime);

    /// <summary>
    /// Cleans up stale pending checkout classes (older than specified minutes).
    /// Called periodically to prevent slots from being held indefinitely.
    /// </summary>
    Task<int> CleanupStalePendingClassesAsync(int olderThanMinutes = 30);
}
