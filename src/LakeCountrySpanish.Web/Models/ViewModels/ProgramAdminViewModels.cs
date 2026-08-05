using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>Row shape for the /Admin/Programs list.</summary>
public sealed class ProgramListItemViewModel
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal FullPrice { get; init; }
    public bool IsActive { get; init; }
    public bool IsListed { get; init; }
    public int EnrollmentCount { get; init; }
    public int PaidCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Combined create/edit form model. Validation attributes are enforced by MVC
/// model binding; additional service-level guards live in
/// <see cref="Services.Programs.EnrollmentProgramService"/>.
/// </summary>
public sealed class ProgramFormViewModel
{
    public int Id { get; set; }

    // ---------- Basics ----------

    [Required, StringLength(80, MinimumLength = 2)]
    [RegularExpression(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$",
        ErrorMessage = "Slug can contain lowercase letters, digits, and hyphens; must start and end alphanumerically.")]
    [Display(Name = "URL slug", Description = "Used in the /join/{slug} URL. Lowercase, hyphens allowed.")]
    public string Slug { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 4)]
    [Display(Name = "Program name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Tagline")]
    public string TagLine { get; set; } = string.Empty;

    [Display(Name = "Description (Markdown)")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Existing image path (edit mode) or path assigned by the controller after
    /// a successful upload (create mode). Hidden from the form directly — Karen
    /// interacts with <see cref="HeroImageUpload"/> and the current image preview.
    /// </summary>
    public string? HeroImagePath { get; set; }

    /// <summary>
    /// Optional file upload from the parent-form multipart POST. Controller
    /// validates + saves to <c>wwwroot/img/programs/{slug}.{ext}</c> and
    /// populates <see cref="HeroImagePath"/> from the saved location.
    /// </summary>
    [Display(Name = "Hero image", Description = "JPG, PNG, or WebP. Optional — a colored gradient shows if you leave this blank.")]
    public IFormFile? HeroImageUpload { get; set; }

    // ---------- Logistics ----------

    [Required, StringLength(120)]
    [Display(Name = "Location name")]
    public string LocationName { get; set; } = string.Empty;

    [Required, StringLength(240)]
    [Display(Name = "Location address")]
    public string LocationAddress { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateTime StartDate { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Derived from the seven weekday checkboxes below on submit; also displayed
    /// back on edit if the stored value doesn't parse to a checkbox pattern.
    /// Stored form: "Tuesdays" / "Mondays & Wednesdays" / "Mondays, Wednesdays & Fridays".
    /// Nullable to opt out of ASP.NET Core's implicit-required-for-non-nullable-strings
    /// behavior — this field is populated server-side from the checkbox booleans in
    /// <see cref="ToEntity"/>, not typed by the admin, so it always posts as empty and
    /// would otherwise trip the implicit Required validator.
    /// </summary>
    [Display(Name = "Meeting days")]
    public string? MeetingDays { get; set; }

    [Display(Name = "Sun")] public bool MeetingDaySun { get; set; }
    [Display(Name = "Mon")] public bool MeetingDayMon { get; set; }
    [Display(Name = "Tue")] public bool MeetingDayTue { get; set; }
    [Display(Name = "Wed")] public bool MeetingDayWed { get; set; }
    [Display(Name = "Thu")] public bool MeetingDayThu { get; set; }
    [Display(Name = "Fri")] public bool MeetingDayFri { get; set; }
    [Display(Name = "Sat")] public bool MeetingDaySat { get; set; }

    [Required, DataType(DataType.Time)]
    [Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; }

    [Required, DataType(DataType.Time)]
    [Display(Name = "End time")]
    public TimeOnly EndTime { get; set; }

    [StringLength(20)]
    [Display(Name = "Grade range", Description = "Free-form: \"3-6\" or \"K-2\".")]
    public string GradeRange { get; set; } = string.Empty;

    [Range(3, 18)]
    [Display(Name = "Min age")]
    public int AgeMin { get; set; }

    [Range(3, 18)]
    [Display(Name = "Max age")]
    public int AgeMax { get; set; }

    // ---------- Pricing ----------

    [Required, Range(1, 10000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Full price")]
    public decimal FullPrice { get; set; }

    [Display(Name = "Offer installment plan")]
    public bool InstallmentsEnabled { get; set; } = true;

    [Range(2, 12)]
    [Display(Name = "Number of installments")]
    public int InstallmentCount { get; set; } = 2;

    [DataType(DataType.Date)]
    [Display(Name = "Final installment due date",
             Description = "Informational — shown to parents so they know when installments finish. Cadence itself is monthly starting at signup.")]
    public DateTime? FinalInstallmentDueDate { get; set; }

    [Display(Name = "Allow cash-in-hand option",
             Description = "Show \"I'll pay Karen at the booth\" as a payment choice. Turn off for online-only enrollment.")]
    public bool CashOptionEnabled { get; set; } = true;

    // ---------- Legal / contact ----------

    [Required]
    [Display(Name = "Waiver text (Markdown)")]
    public string WaiverText { get; set; } = string.Empty;

    [StringLength(400)]
    [Display(Name = "Refund policy")]
    public string RefundPolicyText { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "Contact phone")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(120)]
    [Display(Name = "Contact email")]
    public string ContactEmail { get; set; } = string.Empty;

    // ---------- Status ----------

    [Display(Name = "Accepting enrollments")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Show on public programs calendar",
             Description = "On by default so past + prospective parents can discover it. Turn off for private / invite-only events.")]
    public bool IsListed { get; set; } = true;

    // ---------- Read-only in edit mode ----------

    /// <summary>True when Stripe has already provisioned this program's Product/Prices — freezes price-affecting fields.</summary>
    public bool PricingLocked { get; set; }

    public string? StripeProductId { get; set; }

    public bool IsEdit => Id > 0;
    public string PageTitle => IsEdit ? "Edit program" : "New program";

    public EnrollmentProgram ToEntity(EnrollmentProgram? existing = null)
    {
        var target = existing ?? new EnrollmentProgram();
        target.Id = Id;
        target.Slug = Slug;
        target.Name = Name;
        target.TagLine = TagLine ?? string.Empty;
        target.Description = Description ?? string.Empty;
        target.HeroImagePath = HeroImagePath;
        target.LocationName = LocationName;
        target.LocationAddress = LocationAddress;
        target.StartDate = DateTime.SpecifyKind(StartDate, DateTimeKind.Utc);
        target.EndDate = DateTime.SpecifyKind(EndDate, DateTimeKind.Utc);
        target.MeetingDays = CompileMeetingDays();
        target.StartTime = StartTime;
        target.EndTime = EndTime;
        target.GradeRange = GradeRange ?? string.Empty;
        target.AgeMin = AgeMin;
        target.AgeMax = AgeMax;
        target.FullPrice = FullPrice;
        target.InstallmentsEnabled = InstallmentsEnabled;
        target.InstallmentCount = InstallmentCount;
        target.FinalInstallmentDueDate = FinalInstallmentDueDate.HasValue
            ? DateTime.SpecifyKind(FinalInstallmentDueDate.Value, DateTimeKind.Utc)
            : null;
        target.CashOptionEnabled = CashOptionEnabled;
        target.WaiverText = WaiverText ?? string.Empty;
        target.RefundPolicyText = RefundPolicyText ?? string.Empty;
        target.ContactPhone = ContactPhone;
        target.ContactEmail = ContactEmail;
        target.IsActive = IsActive;
        target.IsListed = IsListed;
        return target;
    }

    public static ProgramFormViewModel FromEntity(EnrollmentProgram p)
    {
        var vm = new ProgramFormViewModel
        {
            Id = p.Id,
            Slug = p.Slug,
            Name = p.Name,
            TagLine = p.TagLine,
            Description = p.Description,
            HeroImagePath = p.HeroImagePath,
            LocationName = p.LocationName,
            LocationAddress = p.LocationAddress,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            MeetingDays = p.MeetingDays,
            StartTime = p.StartTime,
            EndTime = p.EndTime,
            GradeRange = p.GradeRange,
            AgeMin = p.AgeMin,
            AgeMax = p.AgeMax,
            FullPrice = p.FullPrice,
            InstallmentsEnabled = p.InstallmentsEnabled,
            InstallmentCount = p.InstallmentCount,
            FinalInstallmentDueDate = p.FinalInstallmentDueDate,
            CashOptionEnabled = p.CashOptionEnabled,
            WaiverText = p.WaiverText,
            RefundPolicyText = p.RefundPolicyText,
            ContactPhone = p.ContactPhone,
            ContactEmail = p.ContactEmail,
            IsActive = p.IsActive,
            IsListed = p.IsListed,
            PricingLocked = !string.IsNullOrEmpty(p.StripeProductId),
            StripeProductId = p.StripeProductId
        };
        vm.ApplyMeetingDaysFromString(p.MeetingDays);
        return vm;
    }

    /// <summary>
    /// Turns the seven day checkboxes into a display string like
    /// "Tuesdays", "Mondays &amp; Wednesdays", or "Mondays, Wednesdays &amp; Fridays".
    /// Falls back to whatever's in <see cref="MeetingDays"/> when no boxes are checked,
    /// so admins editing legacy free-text records don't get it wiped.
    /// </summary>
    public string CompileMeetingDays()
    {
        var selected = new List<string>(7);
        if (MeetingDaySun) selected.Add("Sundays");
        if (MeetingDayMon) selected.Add("Mondays");
        if (MeetingDayTue) selected.Add("Tuesdays");
        if (MeetingDayWed) selected.Add("Wednesdays");
        if (MeetingDayThu) selected.Add("Thursdays");
        if (MeetingDayFri) selected.Add("Fridays");
        if (MeetingDaySat) selected.Add("Saturdays");

        if (selected.Count == 0) return MeetingDays ?? string.Empty;
        if (selected.Count == 1) return selected[0];
        if (selected.Count == 2) return $"{selected[0]} & {selected[1]}";
        return string.Join(", ", selected.Take(selected.Count - 1)) + " & " + selected[^1];
    }

    /// <summary>
    /// Best-effort parse of a stored MeetingDays string back into checkbox state
    /// for edit mode. Looks for the substring "Mon", "Tue", etc. — matches both
    /// "Mondays" and "Mon, Wed" style. Case-insensitive.
    /// </summary>
    public void ApplyMeetingDaysFromString(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        var s = source;
        MeetingDaySun = s.Contains("Sun", StringComparison.OrdinalIgnoreCase);
        MeetingDayMon = s.Contains("Mon", StringComparison.OrdinalIgnoreCase);
        MeetingDayTue = s.Contains("Tue", StringComparison.OrdinalIgnoreCase);
        MeetingDayWed = s.Contains("Wed", StringComparison.OrdinalIgnoreCase);
        MeetingDayThu = s.Contains("Thu", StringComparison.OrdinalIgnoreCase);
        MeetingDayFri = s.Contains("Fri", StringComparison.OrdinalIgnoreCase);
        MeetingDaySat = s.Contains("Sat", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Printable QR card — QR + label with the info Karen needs to tell
/// two printed cards apart at a booth (program name, dates, location, URL).</summary>
public sealed class ProgramPrintCardViewModel
{
    public EnrollmentProgram Program { get; init; } = null!;
    public string JoinUrl { get; init; } = string.Empty;
    public string QrImageUrl { get; init; } = string.Empty;
}

/// <summary>Detail view — shows program summary, QR code, join URL, enrollment stats.</summary>
public sealed class ProgramDetailViewModel
{
    public EnrollmentProgram Program { get; init; } = null!;
    public string JoinUrl { get; init; } = string.Empty;
    public int EnrollmentCount { get; init; }
    public int PaidCount { get; init; }
    public int PendingCount { get; init; }
    public int CashPendingCount { get; init; }
}

/// <summary>Full roster view for a program — every enrollment, with cash-confirm actions on unpaid cash rows.</summary>
public sealed class ProgramEnrollmentsRosterViewModel
{
    public EnrollmentProgram Program { get; init; } = null!;
    public IReadOnlyList<ProgramEnrollment> Enrollments { get; init; } = Array.Empty<ProgramEnrollment>();
}
