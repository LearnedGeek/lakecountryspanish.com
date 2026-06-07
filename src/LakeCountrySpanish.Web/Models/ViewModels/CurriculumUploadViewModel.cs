using Microsoft.AspNetCore.Http;

namespace LakeCountrySpanish.Web.Models.ViewModels;

public sealed class CurriculumUploadViewModel
{
    /// <summary>
    /// The uploaded .docx file (LCS lesson template).
    /// </summary>
    public IFormFile? DocxFile { get; set; }

    /// <summary>
    /// Optional explicit Unit override. Default behaviour: title-match against
    /// the Unit cell in the parsed Metadata table.
    /// </summary>
    public int? UnitId { get; set; }

    public IReadOnlyList<UnitOption> AvailableUnits { get; set; } = Array.Empty<UnitOption>();

    /// <summary>Surfaced when parse fails or Unit resolution fails.</summary>
    public string? ErrorMessage { get; set; }
}
