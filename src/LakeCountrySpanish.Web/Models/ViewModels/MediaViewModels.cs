using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services.Media;

namespace LakeCountrySpanish.Web.Models.ViewModels;

public sealed class MediaIndexViewModel
{
    public MediaLibraryPage Page { get; init; } = new(Array.Empty<MediaAsset>(), 1, 24, 0);
    public MediaCategory? CategoryFilter { get; init; }
    public MediaSource? SourceFilter { get; init; }
}

public sealed class MediaUploadViewModel
{
    [Required]
    [Display(Name = "Image file")]
    public IFormFile? File { get; set; }

    [Display(Name = "Title")]
    public string? Title { get; set; }

    [Display(Name = "Alt text (accessibility)")]
    public string? AltText { get; set; }

    [Display(Name = "Tags (comma-separated)")]
    public string? Tags { get; set; }

    [Display(Name = "Category")]
    public MediaCategory Category { get; set; } = MediaCategory.Uncategorized;
}

public sealed class PixabayBrowseViewModel
{
    public string? Query { get; init; }
    public ImageContentType ContentType { get; init; } = ImageContentType.All;
    public int Page { get; init; } = 1;
    public ImageSearchResults? Results { get; init; }
    public string? ErrorMessage { get; init; }
    public bool PixabayAvailable { get; init; }
}

/// <summary>
/// Form payload from the "Import this image" button on a Pixabay search
/// result. Carries every field needed to materialize an <see cref="ImageSearchHit"/>
/// so the import doesn't need a second round-trip to Pixabay.
/// </summary>
public sealed class PixabayImportViewModel
{
    [Required] public string SourceId { get; set; } = string.Empty;
    [Required] public string PageUrl { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
    [Required] public string WebUrl { get; set; } = string.Empty;
    public string FullUrl { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Photographer { get; set; } = string.Empty;
    public string PhotographerUrl { get; set; } = string.Empty;
    public string TagsCsv { get; set; } = string.Empty;

    [Display(Name = "Category")]
    public MediaCategory Category { get; set; } = MediaCategory.Uncategorized;

    [Display(Name = "Alt text")]
    public string? AltText { get; set; }

    [Display(Name = "Title")]
    public string? Title { get; set; }

    public ImageSearchHit ToSearchHit() => new(
        SourceId: SourceId,
        PageUrl: PageUrl,
        PreviewUrl: PreviewUrl,
        WebUrl: WebUrl,
        FullUrl: FullUrl,
        Width: Width,
        Height: Height,
        Photographer: Photographer,
        PhotographerUrl: PhotographerUrl,
        LicenseType: "Pixabay License",
        Tags: string.IsNullOrWhiteSpace(TagsCsv)
            ? Array.Empty<string>()
            : TagsCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}
