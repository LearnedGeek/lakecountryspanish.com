using SpanishScheduler.Web.Models.Entities;

namespace SpanishScheduler.Web.Services;

public interface IPaymentService
{
    Task<string> CreateCheckoutSessionAsync(string studentId, int? packageId, int? classId, decimal amount, string successUrl, string cancelUrl);
    Task<Payment?> ProcessWebhookAsync(string json, string stripeSignature);
    Task<Payment?> GetPaymentBySessionIdAsync(string sessionId);
    Task<IEnumerable<Payment>> GetStudentPaymentsAsync(string studentId);
    Task<IEnumerable<Payment>> GetAllPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null, string? studentId = null);
    Task<decimal> GetStudentBalanceAsync(string studentId);
    Task<decimal> GetClassPriceForStudentAsync(string studentId);
}
