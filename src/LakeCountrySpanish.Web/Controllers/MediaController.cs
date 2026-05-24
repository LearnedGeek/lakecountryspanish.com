using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class MediaController : Controller
{
    private const int DefaultPageSize = 24;

    private readonly IMediaService _media;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEnumerable<IImageSourceAdapter> _sourceAdapters;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        IMediaService media,
        UserManager<ApplicationUser> userManager,
        IEnumerable<IImageSourceAdapter> sourceAdapters,
        ILogger<MediaController> logger)
    {
        _media = media;
        _userManager = userManager;
        _sourceAdapters = sourceAdapters;
        _logger = logger;
    }

    public async Task<IActionResult> Index(int page = 1, MediaCategory? category = null, MediaSource? source = null)
    {
        var libraryPage = await _media.ListAsync(page, DefaultPageSize, category, source);
        return View(new MediaIndexViewModel
        {
            Page = libraryPage,
            CategoryFilter = category,
            SourceFilter = source
        });
    }

    [HttpGet]
    public IActionResult Upload() => View(new MediaUploadViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(MediaUploadViewModel model)
    {
        if (model.File is null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), "Please choose an image file.");
            return View(model);
        }

        if (!ModelState.IsValid) return View(model);

        using var ms = new MemoryStream();
        await model.File.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var userId = _userManager.GetUserId(User) ?? string.Empty;

        try
        {
            var asset = await _media.UploadAsync(new UploadMediaRequest
            {
                Bytes = bytes,
                OriginalFileName = model.File.FileName,
                MimeType = model.File.ContentType,
                Category = model.Category,
                Title = model.Title,
                AltText = model.AltText,
                Tags = model.Tags,
                UploadedById = userId
            });

            TempData["SuccessMessage"] = $"Uploaded \"{asset.Title}\" to the media library.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for {FileName}", model.File.FileName);
            ModelState.AddModelError(string.Empty, "Upload failed: " + ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> BrowsePixabay(string? q, ImageContentType contentType = ImageContentType.All, int page = 1)
    {
        var adapter = _sourceAdapters.FirstOrDefault(a => a.SourceEnum == MediaSource.Pixabay);
        var available = adapter?.IsAvailable ?? false;

        PixabayBrowseViewModel Build(ImageSearchResults? results = null, string? error = null) => new()
        {
            Query = q,
            ContentType = contentType,
            Page = page,
            PixabayAvailable = available,
            Results = results,
            ErrorMessage = error
        };

        if (adapter is null || !adapter.IsAvailable)
        {
            return View(Build(error: "Pixabay is not configured. Add the API key under \"Pixabay:ApiKey\" in appsettings.Local.json."));
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return View(Build());
        }

        try
        {
            var results = await adapter.SearchAsync(new ImageSearchQuery(q, page, DefaultPageSize, contentType));
            return View(Build(results: results));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Pixabay search failed for query {Query}", q);
            return View(Build(error: "Pixabay search failed: " + ex.Message));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportPixabay(PixabayImportViewModel model, string? returnQuery = null)
    {
        var adapter = _sourceAdapters.FirstOrDefault(a => a.SourceEnum == MediaSource.Pixabay);
        if (adapter is null || !adapter.IsAvailable)
        {
            TempData["ErrorMessage"] = "Pixabay is not configured.";
            return RedirectToAction(nameof(BrowsePixabay), new { q = returnQuery });
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;

        try
        {
            var asset = await _media.ImportFromSourceAsync(
                adapter, model.ToSearchHit(),
                new ImportMediaOptions
                {
                    Category = model.Category,
                    Title = model.Title,
                    AltText = model.AltText,
                    Tags = model.TagsCsv,
                    ImportedById = userId
                });

            TempData["SuccessMessage"] = $"Imported \"{asset.Title}\" from Pixabay.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pixabay import failed for sourceId={SourceId}", model.SourceId);
            TempData["ErrorMessage"] = "Pixabay import failed: " + ex.Message;
        }

        return RedirectToAction(nameof(BrowsePixabay), new { q = returnQuery });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _media.DeleteAsync(id);
            TempData["SuccessMessage"] = "Media asset deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
