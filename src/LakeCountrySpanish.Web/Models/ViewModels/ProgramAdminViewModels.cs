using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
/// Combined create/edit form model. Two save modes:
/// <list type="bullet">
///   <item><b>Draft</b> — <see cref="PublishOnSave"/> = false. Model binder still
///   validates content when present (Range, StringLength, EmailAddress, etc.)
///   but skipping-fields-entirely is allowed so Karen can save partial progress.</item>
///   <item><b>Publish</b> — <see cref="PublishOnSave"/> = true. Adds "required to
///   publish" errors via <see cref="IValidatableObject.Validate"/>, provisions
///   Stripe on the service side, and flips <see cref="IsActive"/> true.</item>
/// </list>
/// The <see cref="Slug"/> stays required in both modes because we need it to
/// route back to the edit page. Everything else that a public-facing enrollment
/// would need is allowed to be blank in draft mode.
/// </summary>
public sealed class ProgramFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    /// <summary>
    /// Set by the controller from the form's submit button (Save draft vs
    /// Save &amp; Publish). Drives <see cref="Validate"/> to add the strict
    /// "required to publish" errors only when the admin means to go live.
    /// Never bound directly from form input to avoid a client shipping
    /// publish=true and bypassing intent.
    /// </summary>
    [BindNever]
    public bool PublishOnSave { get; set; }

    // ---------- Basics ----------

    [Required, StringLength(80, MinimumLength = 2)]
    [RegularExpression(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$",
        ErrorMessage = "Slug can contain lowercase letters, digits, and hyphens; must start and end alphanumerically.")]
    [Display(Name = "URL slug", Description = "Used in the /join/{slug} URL. Lowercase, hyphens allowed.")]
    public string Slug { get; set; } = string.Empty;

    // Name is required to publish but not to save a draft (Karen may be
    // sketching several programs in parallel and hasn't settled on names).
    [StringLength(160, MinimumLength = 2)]
    [Display(Name = "Program name")]
    public string? Name { get; set; }

    [StringLength(200)]
    [Display(Name = "Tagline")]
    public string? TagLine { get; set; }

    [Display(Name = "Description (Markdown)")]
    public string? Description { get; set; }

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
    [Display(Name = "Hero / banner image", Description = "JPG, PNG, or WebP. Optional — a colored gradient shows if you leave this blank. Used as the big banner at the top of the details and enrollment pages. May be a general brand image reused across programs.")]
    public IFormFile? HeroImageUpload { get; set; }

    /// <summary>Existing event image path (edit mode) or path assigned after upload.</summary>
    public string? EventImagePath { get; set; }

    /// <summary>
    /// Optional program-specific image (e.g. a ChatGPT illustration Karen
    /// generates for this offering). Saved to
    /// <c>wwwroot/img/programs/{slug}-event.{ext}</c>.
    /// </summary>
    [Display(Name = "Event image", Description = "JPG, PNG, or WebP. Optional. Used as the card visual on the /programs page and inline on the details page. This is the program-specific image (Karen's ChatGPT illustrations, event photos, etc.).")]
    public IFormFile? EventImageUpload { get; set; }

    // ---------- Logistics ----------

    [StringLength(120)]
    [Display(Name = "Location name")]
    public string? LocationName { get; set; }

    [StringLength(240)]
    [Display(Name = "Location address")]
    public string? LocationAddress { get; set; }

    // Dates/times are nullable so Karen can save a draft without them. On
    // publish, Validate() requires all four (StartDate, EndDate, StartTime,
    // EndTime) to be set and coherent.
    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateTime? EndDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Enrollment opens",
             Description = "Optional. When set to a future date, the program appears on /programs under \"Coming soon\" until this date. Leave blank to accept enrollments immediately.")]
    public DateTime? EnrollmentStartsAt { get; set; }

    [Display(Name = "Enrollment deadline",
             Description = "Optional. When set, parents can't enroll after this date. Leave blank to keep enrollment open until the program starts.")]
    public DateTime? EnrollmentDeadline { get; set; }

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

    [DataType(DataType.Time)]
    [Display(Name = "Start time")]
    public TimeOnly? StartTime { get; set; }

    [DataType(DataType.Time)]
    [Display(Name = "End time")]
    public TimeOnly? EndTime { get; set; }

    [StringLength(20)]
    [Display(Name = "Grade range", Description = "Free-form: \"3-6\", \"K-2\", or leave blank for adult / no-restriction programs.")]
    public string? GradeRange { get; set; }

    // 0 = "no restriction" (e.g. adult programs Karen doesn't want to gate by age).
    // Display views hide the "· ages X–Y" text when AgeMin is 0.
    [Range(0, 120)]
    [Display(Name = "Min age", Description = "Enter 0 to indicate no age restriction (e.g. adult programs).")]
    public int AgeMin { get; set; }

    [Range(0, 120)]
    [Display(Name = "Max age")]
    public int AgeMax { get; set; }

    // ---------- Pricing ----------

    // Nullable so Karen can save a draft without pricing decided; still
    // validated for a sane range whenever a value IS present (i.e. content
    // validation still runs, we just don't require the field to exist).
    [Range(1, 10000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Full price")]
    public decimal? FullPrice { get; set; }

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

    [Display(Name = "Waiver text (Markdown)")]
    public string? WaiverText { get; set; }

    [StringLength(400)]
    [Display(Name = "Refund policy")]
    public string? RefundPolicyText { get; set; }

    [StringLength(20)]
    [Display(Name = "Contact phone")]
    public string? ContactPhone { get; set; }

    [EmailAddress, StringLength(120)]
    [Display(Name = "Contact email")]
    public string? ContactEmail { get; set; }

    // ---------- Status ----------

    // Defaults to false so a bare-hands Create starts as a draft. Publish
    // flips it via the Save & Publish button (or the Publish action on
    // Detail). Karen can also toggle this manually in the form.
    [Display(Name = "Accepting enrollments")]
    public bool IsActive { get; set; }

    [Display(Name = "Show on public programs calendar",
             Description = "On by default so past + prospective parents can discover it. Turn off for private / invite-only events.")]
    public bool IsListed { get; set; } = true;

    // ---------- Read-only in edit mode ----------

    /// <summary>True when Stripe has already provisioned this program's Product/Prices — freezes price-affecting fields.</summary>
    public bool PricingLocked { get; set; }

    public string? StripeProductId { get; set; }

    public bool IsEdit => Id > 0;
    public string PageTitle => IsEdit ? "Edit program" : "New program";

    /// <summary>
    /// Publish-time strict validation. Only runs when <see cref="PublishOnSave"/>
    /// is true — draft saves skip these checks so Karen can save partial work.
    /// Field-level content validation (Range, StringLength, EmailAddress, etc.)
    /// runs regardless via data annotations.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!PublishOnSave) yield break;

        if (string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult("Program name is required to publish.", new[] { nameof(Name) });
        if (string.IsNullOrWhiteSpace(LocationName))
            yield return new ValidationResult("Location name is required to publish.", new[] { nameof(LocationName) });
        if (string.IsNullOrWhiteSpace(LocationAddress))
            yield return new ValidationResult("Location address is required to publish.", new[] { nameof(LocationAddress) });
        if (!StartDate.HasValue)
            yield return new ValidationResult("Start date is required to publish.", new[] { nameof(StartDate) });
        if (!EndDate.HasValue)
            yield return new ValidationResult("End date is required to publish.", new[] { nameof(EndDate) });
        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
            yield return new ValidationResult("End date must be after start date.", new[] { nameof(EndDate) });
        if (!StartTime.HasValue)
            yield return new ValidationResult("Start time is required to publish.", new[] { nameof(StartTime) });
        if (!EndTime.HasValue)
            yield return new ValidationResult("End time is required to publish.", new[] { nameof(EndTime) });
        if (!FullPrice.HasValue || FullPrice.Value <= 0)
            yield return new ValidationResult("Full price is required to publish (must be positive).", new[] { nameof(FullPrice) });
        if (string.IsNullOrWhiteSpace(WaiverText))
            yield return new ValidationResult("Waiver text is required to publish.", new[] { nameof(WaiverText) });
        if (string.IsNullOrWhiteSpace(ContactPhone))
            yield return new ValidationResult("Contact phone is required to publish.", new[] { nameof(ContactPhone) });
        if (string.IsNullOrWhiteSpace(ContactEmail))
            yield return new ValidationResult("Contact email is required to publish.", new[] { nameof(ContactEmail) });
    }

    public EnrollmentProgram ToEntity(EnrollmentProgram? existing = null)
    {
        var target = existing ?? new EnrollmentProgram();
        target.Id = Id;
        target.Slug = Slug;
        // Fields that admit blanks in draft mode default to empty string on the
        // entity (which stays non-nullable to keep the public read paths simple).
        target.Name = Name ?? string.Empty;
        target.TagLine = TagLine ?? string.Empty;
        target.Description = Description ?? string.Empty;
        target.HeroImagePath = HeroImagePath;
        target.EventImagePath = EventImagePath;
        target.LocationName = LocationName ?? string.Empty;
        target.LocationAddress = LocationAddress ?? string.Empty;
        // Value-typed fields (dates/times/price) fall back to sentinel default
        // when not set on a draft — public views never see these because drafts
        // are IsActive=false and filtered out of every public query.
        target.StartDate = StartDate.HasValue
            ? DateTime.SpecifyKind(StartDate.Value, DateTimeKind.Utc)
            : DateTime.MinValue;
        target.EndDate = EndDate.HasValue
            ? DateTime.SpecifyKind(EndDate.Value, DateTimeKind.Utc)
            : DateTime.MinValue;
        target.EnrollmentDeadline = EnrollmentDeadline.HasValue
            ? DateTime.SpecifyKind(EnrollmentDeadline.Value, DateTimeKind.Utc)
            : null;
        target.EnrollmentStartsAt = EnrollmentStartsAt.HasValue
            ? DateTime.SpecifyKind(EnrollmentStartsAt.Value, DateTimeKind.Utc)
            : null;
        target.MeetingDays = CompileMeetingDays();
        target.StartTime = StartTime ?? default;
        target.EndTime = EndTime ?? default;
        target.GradeRange = GradeRange ?? string.Empty;
        target.AgeMin = AgeMin;
        target.AgeMax = AgeMax;
        target.FullPrice = FullPrice ?? 0m;
        target.InstallmentsEnabled = InstallmentsEnabled;
        target.InstallmentCount = InstallmentCount;
        target.FinalInstallmentDueDate = FinalInstallmentDueDate.HasValue
            ? DateTime.SpecifyKind(FinalInstallmentDueDate.Value, DateTimeKind.Utc)
            : null;
        target.CashOptionEnabled = CashOptionEnabled;
        target.WaiverText = WaiverText ?? string.Empty;
        target.RefundPolicyText = RefundPolicyText ?? string.Empty;
        target.ContactPhone = ContactPhone ?? string.Empty;
        target.ContactEmail = ContactEmail ?? string.Empty;
        target.IsActive = IsActive;
        target.IsListed = IsListed;
        return target;
    }

    public static ProgramFormViewModel FromEntity(EnrollmentProgram p)
    {
        // Roundtrip sentinel defaults back to null so the form doesn't render
        // MinValue / 0 as if the admin actually typed them for a draft.
        var vm = new ProgramFormViewModel
        {
            Id = p.Id,
            Slug = p.Slug,
            Name = string.IsNullOrEmpty(p.Name) ? null : p.Name,
            TagLine = string.IsNullOrEmpty(p.TagLine) ? null : p.TagLine,
            Description = string.IsNullOrEmpty(p.Description) ? null : p.Description,
            HeroImagePath = p.HeroImagePath,
            EventImagePath = p.EventImagePath,
            LocationName = string.IsNullOrEmpty(p.LocationName) ? null : p.LocationName,
            LocationAddress = string.IsNullOrEmpty(p.LocationAddress) ? null : p.LocationAddress,
            StartDate = p.StartDate == DateTime.MinValue ? null : p.StartDate,
            EndDate = p.EndDate == DateTime.MinValue ? null : p.EndDate,
            EnrollmentDeadline = p.EnrollmentDeadline,
            EnrollmentStartsAt = p.EnrollmentStartsAt,
            MeetingDays = p.MeetingDays,
            StartTime = p.StartTime == default ? null : p.StartTime,
            EndTime = p.EndTime == default ? null : p.EndTime,
            GradeRange = p.GradeRange,
            AgeMin = p.AgeMin,
            AgeMax = p.AgeMax,
            FullPrice = p.FullPrice == 0m ? null : p.FullPrice,
            InstallmentsEnabled = p.InstallmentsEnabled,
            InstallmentCount = p.InstallmentCount,
            FinalInstallmentDueDate = p.FinalInstallmentDueDate,
            CashOptionEnabled = p.CashOptionEnabled,
            WaiverText = string.IsNullOrEmpty(p.WaiverText) ? null : p.WaiverText,
            RefundPolicyText = string.IsNullOrEmpty(p.RefundPolicyText) ? null : p.RefundPolicyText,
            ContactPhone = string.IsNullOrEmpty(p.ContactPhone) ? null : p.ContactPhone,
            ContactEmail = string.IsNullOrEmpty(p.ContactEmail) ? null : p.ContactEmail,
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

    /// <summary>
    /// Audit events grouped by enrollment id, oldest first. Populated for every
    /// enrollment in the roster so the view can render the trail inline without
    /// N+1 queries. Empty list for enrollments with no audited actions yet.
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<ProgramEnrollmentAuditEvent>> AuditEventsByEnrollmentId { get; init; }
        = new Dictionary<int, IReadOnlyList<ProgramEnrollmentAuditEvent>>();
}
