using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

public class PaymentHistoryViewModel
{
    public IEnumerable<Payment> Payments { get; set; } = new List<Payment>();
    public decimal TotalPaid { get; set; }
    public decimal OutstandingBalance { get; set; }
}

public class CheckoutViewModel
{
    public int? PackageId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public Package? Package { get; set; }
    public decimal Savings { get; set; }
}

public class AdminPaymentsViewModel
{
    public IEnumerable<Payment> Payments { get; set; } = new List<Payment>();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StudentId { get; set; }
    public IEnumerable<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
    public decimal TotalRevenue { get; set; }
    public decimal PendingPayments { get; set; }
}
