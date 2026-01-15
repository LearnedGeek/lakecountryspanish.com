using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Result of processing a webhook event.
/// </summary>
public class WebhookProcessingResult
{
    public bool Success { get; set; }
    public bool IsSignatureInvalid { get; set; }
    public bool AlreadyProcessed { get; set; }
    public string? ErrorMessage { get; set; }
    public Payment? Payment { get; set; }

    public static WebhookProcessingResult Succeeded(Payment? payment = null) =>
        new() { Success = true, Payment = payment };

    public static WebhookProcessingResult SignatureInvalid() =>
        new() { Success = false, IsSignatureInvalid = true, ErrorMessage = "Invalid webhook signature" };

    public static WebhookProcessingResult Duplicate() =>
        new() { Success = true, AlreadyProcessed = true };

    public static WebhookProcessingResult Failed(string error) =>
        new() { Success = false, ErrorMessage = error };

    public static WebhookProcessingResult Skipped(string reason) =>
        new() { Success = true, ErrorMessage = reason };
}

public interface IPaymentService
{
    Task<string> CreateCheckoutSessionAsync(string studentId, int? packageId, int? classId, decimal amount, string successUrl, string cancelUrl);
    Task<WebhookProcessingResult> ProcessWebhookAsync(string json, string stripeSignature);
    Task<Payment?> GetPaymentBySessionIdAsync(string sessionId);
    Task<IEnumerable<Payment>> GetStudentPaymentsAsync(string studentId);
    Task<IEnumerable<Payment>> GetAllPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null, string? studentId = null);
    Task<decimal> GetStudentBalanceAsync(string studentId);
    Task<decimal> GetClassPriceForStudentAsync(string studentId);

    // Tip-related methods
    Task<string?> CreateTipCheckoutSessionAsync(string studentId, decimal amount, string? message, int? classId);
    Task<bool> ProcessTipWebhookAsync(string sessionId);
}
