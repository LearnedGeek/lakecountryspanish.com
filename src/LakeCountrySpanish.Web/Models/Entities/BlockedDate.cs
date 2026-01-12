namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Represents a blocked date range when the teacher is unavailable (vacation, holiday, etc.)
/// Students cannot schedule classes during blocked periods.
/// </summary>
public class BlockedDate
{
    public int Id { get; set; }

    /// <summary>
    /// Start date of the blocked period (inclusive)
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the blocked period (inclusive)
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Reason for blocking (e.g., "Vacation", "Holiday", "Personal")
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// When this blocked period was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
