using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

public class ContactViewModel
{
    [Required]
    [Display(Name = "Your Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Phone Number (Optional)")]
    public string? Phone { get; set; }

    [Required]
    [MinLength(10)]
    [Display(Name = "Message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Honeypot field - should always be empty. Bots will fill this in.
    /// </summary>
    public string? Website { get; set; }
}

public class AdminInquiriesViewModel
{
    public IEnumerable<ContactInquiry> Inquiries { get; set; } = new List<ContactInquiry>();
    public InquiryStatus? FilterStatus { get; set; }
}
