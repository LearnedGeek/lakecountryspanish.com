using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// Public /programs page — programs grouped by enrollment state so parents
/// see actionable offerings first, then upcoming-but-closed, then a history
/// trail of past programs (for social proof / next-year recruiting).
///
/// The Enrolling Now bucket is further grouped by program name so parents see
/// one card per distinct offering with all its locations/sessions rostered
/// inside — this handles the "we run Bailamos at 5 schools" case without
/// showing 5 near-identical hero tiles. A stats banner above the grouped
/// cards shows the aggregate breadth (N sessions across M schools) so the
/// "wow they run a lot" signal isn't lost to the dedupe.
/// </summary>
public sealed class ProgramsCalendarViewModel
{
    /// <summary>
    /// Aggregate stats for the currently-enrolling programs. Renders as a
    /// single strip above the grouped cards.
    /// </summary>
    public EnrollmentOpenStats OpenStats { get; init; } = new();

    /// <summary>
    /// One entry per distinct program name currently enrolling. Each group
    /// carries the full list of sessions (per-location instances) so the card
    /// can render an inline roster.
    /// </summary>
    public IReadOnlyList<EnrollmentOpenGroup> EnrollmentOpenGroups { get; init; } = Array.Empty<EnrollmentOpenGroup>();

    /// <summary>
    /// Programs whose enrollment window hasn't opened yet (EnrollmentStartsAt
    /// is in the future). Shown right below Enrolling Now so parents can see
    /// what's on the horizon and note the opening date.
    /// </summary>
    public IReadOnlyList<EnrollmentProgram> ComingSoon { get; init; } = Array.Empty<EnrollmentProgram>();

    /// <summary>
    /// Programs whose enrollment window has closed but whose end date hasn't
    /// arrived yet — students already enrolled are still meeting; new signups
    /// aren't accepted. Shown so parents know these programs exist and can
    /// contact us for waitlist / next session interest.
    /// </summary>
    public IReadOnlyList<EnrollmentProgram> ClosedButUpcoming { get; init; } = Array.Empty<EnrollmentProgram>();

    /// <summary>Past programs — kept visible as social proof + so parents can request the next run.</summary>
    public IReadOnlyList<EnrollmentProgram> Past { get; init; } = Array.Empty<EnrollmentProgram>();

    public bool HasAny =>
        EnrollmentOpenGroups.Count > 0
        || ComingSoon.Count > 0
        || ClosedButUpcoming.Count > 0
        || Past.Count > 0;
}

/// <summary>
/// One distinct program name with all its currently-enrolling sessions
/// (per-location instances) rostered inside. The Representative is the
/// soonest-starting session and is used for shared display fields (image,
/// description, tagline, grade range).
/// </summary>
public sealed class EnrollmentOpenGroup
{
    public EnrollmentProgram Representative { get; init; } = null!;
    public IReadOnlyList<EnrollmentProgram> Sessions { get; init; } = Array.Empty<EnrollmentProgram>();
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }

    /// <summary>Formatted "$150" if flat, "$125–$200" if the sessions vary.</summary>
    public string PriceLabel => MinPrice == MaxPrice
        ? $"${MinPrice:N2}"
        : $"${MinPrice:N2}–${MaxPrice:N2}";
}

/// <summary>
/// Aggregate marketing headline for the Enrolling Now block. Rolls up so a
/// parent instantly sees breadth ("6 sessions across 4 schools starting
/// Oct 6") without having to visually add up cards.
/// </summary>
public sealed class EnrollmentOpenStats
{
    public int SessionCount { get; init; }
    public int DistinctLocationCount { get; init; }
    public int DistinctProgramCount { get; init; }
    public DateTime? SoonestStart { get; init; }
    public DateTime? LatestEnd { get; init; }

    public bool HasContent => SessionCount > 0;
}
