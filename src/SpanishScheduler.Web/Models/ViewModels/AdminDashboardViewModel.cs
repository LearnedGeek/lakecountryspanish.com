using SpanishScheduler.Web.Models.Entities;

namespace SpanishScheduler.Web.Models.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int TodaysClasses { get; set; }
    public int UpcomingClassesThisWeek { get; set; }
    public decimal TotalRevenueThisMonth { get; set; }
    public decimal OutstandingPayments { get; set; }
    public int NewInquiries { get; set; }
    public IEnumerable<ScheduledClass> TodaysSchedule { get; set; } = new List<ScheduledClass>();
    public IEnumerable<ContactInquiry> RecentInquiries { get; set; } = new List<ContactInquiry>();
    public IEnumerable<Payment> RecentPayments { get; set; } = new List<Payment>();
}
