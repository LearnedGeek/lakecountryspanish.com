using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services.Programs;

/// <summary>
/// Formats a program's audience (grade bands + age range + audience type)
/// into human-readable labels for the public and admin surfaces. Detects
/// contiguous grade-band ranges and prints them as friendly labels
/// (e.g. "K–2", "3–6") instead of enumerating every band.
/// See issue #19 for the design decisions.
/// </summary>
public static class ProgramAudienceDisplay
{
    /// <summary>
    /// Grade portion only. Returns null when the audience type has no
    /// meaningful grade label (Adult, All, or Grades with an empty band
    /// list). Callers combining grade + age handle both parts and the
    /// separator themselves.
    /// </summary>
    public static string? GradeLabel(AudienceType audience, IEnumerable<GradeBand> bands)
    {
        if (audience != AudienceType.Grades) return null;

        var ordered = bands.Distinct().OrderBy(b => (int)b).ToList();
        if (ordered.Count == 0) return null;

        // If the whole span is filled, use the compact "K–8" shortcut.
        if (ordered.Count == 10) return "Grades K–8";

        // Single band → "Grades K" or "Grades 3" (no range formatting).
        if (ordered.Count == 1) return $"Grades {FormatBand(ordered[0])}";

        // Contiguous set → "Grades X–Y" with smart K handling. If both
        // endpoints format to the same string (e.g. K4 + K5 both collapse
        // to "K"), the "range" is really just one label — show it once
        // rather than "K–K".
        if (IsContiguous(ordered))
        {
            var firstLabel = FormatBand(ordered[0]);
            var lastLabel = FormatBand(ordered[^1]);
            return firstLabel == lastLabel
                ? $"Grades {firstLabel}"
                : $"Grades {firstLabel}–{lastLabel}";
        }

        // Non-contiguous → "Grades X, Y, Z" (deduped so K4 + K5 doesn't print "K, K").
        return "Grades " + string.Join(", ", ordered.Select(FormatBand).Distinct());
    }

    /// <summary>
    /// Age portion only. Returns "Ages 18+" for adult programs when the
    /// upper bound is Karen's 99 sentinel, "Ages X–Y" when both are set,
    /// or null when age concept doesn't apply.
    /// </summary>
    public static string? AgeLabel(AudienceType audience, int ageMin, int ageMax)
    {
        if (audience == AudienceType.All) return null;

        if (audience == AudienceType.Adult)
        {
            return ageMax >= 99
                ? $"Ages {ageMin}+"
                : $"Ages {ageMin}–{ageMax}";
        }

        // Grades path: show only when both ends are meaningful.
        if (ageMin <= 0 || ageMax <= 0) return null;
        return $"ages {ageMin}–{ageMax}";
    }

    /// <summary>
    /// Combined label used on public cards and detail sidebars. Handles
    /// the audience-type branching and joins grade + age with " · " when
    /// both are present.
    /// </summary>
    public static string CombinedLabel(AudienceType audience, IEnumerable<GradeBand> bands, int ageMin, int ageMax)
    {
        var parts = new List<string>(2);

        switch (audience)
        {
            case AudienceType.Adult:
                var adultAge = AgeLabel(audience, ageMin, ageMax);
                parts.Add(adultAge is not null ? $"Adults · {adultAge}" : "Adults");
                break;

            case AudienceType.All:
                parts.Add("All ages");
                break;

            case AudienceType.Grades:
                var grades = GradeLabel(audience, bands);
                if (grades is not null) parts.Add(grades);
                var age = AgeLabel(audience, ageMin, ageMax);
                if (age is not null) parts.Add(age);
                if (parts.Count == 0) parts.Add("All ages"); // fallback for Grades with no bands set yet
                break;
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Convenience overload for callers that only have an
    /// <see cref="EnrollmentProgram"/> loaded with its GradeBands navigation.
    /// </summary>
    public static string CombinedLabel(EnrollmentProgram program)
        => CombinedLabel(
            program.AudienceType,
            program.GradeBands.Select(pgb => pgb.GradeBand),
            program.AgeMin,
            program.AgeMax);

    private static bool IsContiguous(IReadOnlyList<GradeBand> orderedBands)
    {
        for (var i = 1; i < orderedBands.Count; i++)
        {
            if ((int)orderedBands[i] - (int)orderedBands[i - 1] != 1)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Formats a single grade band for display. K4/K5 collapse to "K" when
    /// they appear as a range endpoint next to elementary grades, and are
    /// spelled out only when they stand alone.
    /// </summary>
    private static string FormatBand(GradeBand band) => band switch
    {
        GradeBand.K4 => "K",
        GradeBand.K5 => "K",
        GradeBand.Grade1 => "1",
        GradeBand.Grade2 => "2",
        GradeBand.Grade3 => "3",
        GradeBand.Grade4 => "4",
        GradeBand.Grade5 => "5",
        GradeBand.Grade6 => "6",
        GradeBand.Grade7 => "7",
        GradeBand.Grade8 => "8",
        _ => band.ToString()
    };
}
