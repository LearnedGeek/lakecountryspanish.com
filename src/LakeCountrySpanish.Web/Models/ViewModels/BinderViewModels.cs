using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>
/// List view for /Curriculum/Binders — teachers see all documents,
/// grouped by curriculum family. Includes the count of programs currently
/// using each family so admins can see reach at a glance.
/// </summary>
public sealed class BinderListViewModel
{
    public IReadOnlyList<BinderFamilyGroup> FamilyGroups { get; init; } = Array.Empty<BinderFamilyGroup>();
    public IReadOnlyList<string> FamiliesWithoutBinders { get; init; } = Array.Empty<string>();
}

public sealed class BinderFamilyGroup
{
    public string CurriculumFamily { get; init; } = string.Empty;
    public int ProgramCount { get; init; }
    public IReadOnlyList<CurriculumDocument> Documents { get; init; } = Array.Empty<CurriculumDocument>();
}

/// <summary>
/// Admin upload / edit form. New uploads pick a family + doctype;
/// re-uploads to an existing (family, doctype) tuple replace in place.
/// </summary>
public sealed class BinderUploadViewModel : IValidatableObject
{
    // Slug pattern doubles as security perimeter: the value becomes a
    // directory name under the binder storage root, so anything more
    // permissive than [a-z0-9-] risks path traversal even after
    // ToLowerInvariant/Trim. Enforced identically in
    // CurriculumDocumentService as defence-in-depth.
    private static readonly Regex FamilySlugPattern =
        new(@"^[a-z][a-z0-9-]{0,79}$", RegexOptions.Compiled);

    [Required, StringLength(80)]
    [Display(Name = "Curriculum family", Description = "Pick an existing family or type a new one (lowercase-hyphenated, e.g. \"bailamos\").")]
    public string CurriculumFamily { get; set; } = string.Empty;

    [Display(Name = "Document type")]
    public CurriculumDocumentType DocumentType { get; set; } = CurriculumDocumentType.TeacherBinder;

    [StringLength(200)]
    [Display(Name = "Title", Description = "Optional — defaults to the uploaded filename.")]
    public string? Title { get; set; }

    [Display(Name = "Grade bands")]
    public List<GradeBand> SelectedGradeBands { get; set; } = new();

    [Display(Name = "PDF file")]
    public IFormFile? Upload { get; set; }

    /// <summary>Populated by the controller for the creatable dropdown.</summary>
    public IReadOnlyList<string> ExistingCurriculumFamilies { get; set; } = Array.Empty<string>();

    /// <summary>
    /// True when editing an existing (family, doctype) — the form pre-fills
    /// the current selection and treats a fresh Upload as a replace-in-place.
    /// A missing Upload means "keep the current file, just update metadata".
    /// </summary>
    public bool IsReplace { get; set; }

    /// <summary>Existing document being replaced (for display), null on fresh upload.</summary>
    public CurriculumDocument? Existing { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        // Slug format — normalize to lowercase before checking so Karen
        // can paste "Bailamos" into the box without a wall of red.
        var normalizedFamily = (CurriculumFamily ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedFamily.Length > 0 && !FamilySlugPattern.IsMatch(normalizedFamily))
        {
            yield return new ValidationResult(
                "Curriculum family must be lowercase letters, digits, and hyphens (starting with a letter), e.g. \"bailamos\".",
                new[] { nameof(CurriculumFamily) });
        }

        // Reject forged DocumentType values (e.g. crafted POST with an
        // out-of-range enum int) — model binding accepts any int for an
        // enum property.
        if (!Enum.IsDefined(typeof(CurriculumDocumentType), DocumentType))
        {
            yield return new ValidationResult(
                "Unknown document type.", new[] { nameof(DocumentType) });
        }

        // At least one grade band required so teacher list can filter meaningfully.
        if (SelectedGradeBands.Count == 0)
        {
            yield return new ValidationResult("Pick at least one grade band this binder applies to.", new[] { nameof(SelectedGradeBands) });
        }

        // Fresh upload MUST include a file. Replace-in-place can omit it
        // (keeps the current file, just updates title / bands).
        if (!IsReplace && Upload is null)
        {
            yield return new ValidationResult("Please select a PDF to upload.", new[] { nameof(Upload) });
        }

        if (Upload is not null)
        {
            // Empty file is non-null but useless as a binder — teachers
            // would download a zero-byte "PDF" that PDF readers reject.
            if (Upload.Length == 0)
            {
                yield return new ValidationResult("The selected file is empty.", new[] { nameof(Upload) });
            }

            var ext = Path.GetExtension(Upload.FileName).ToLowerInvariant();
            if (ext != ".pdf")
            {
                yield return new ValidationResult("Only PDF files are supported.", new[] { nameof(Upload) });
            }
            const long maxBytes = 25 * 1024 * 1024;
            if (Upload.Length > maxBytes)
            {
                yield return new ValidationResult($"File too large — {Upload.Length / 1024 / 1024} MB. Cap is 25 MB.", new[] { nameof(Upload) });
            }
        }
    }
}
