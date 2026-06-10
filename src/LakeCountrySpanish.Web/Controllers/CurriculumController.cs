using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using LakeCountrySpanish.Web.Services.Curriculum;
using LakeCountrySpanish.Web.Services.Curriculum.Blocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Teacher}")]
public class CurriculumController : Controller
{
    private readonly IDocumentRenderingService _renderer;
    private readonly ICurriculumDayService _days;
    private readonly IBlockCompiler _blocks;
    private readonly DocxLessonParser _parser;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurriculumController(
        IDocumentRenderingService renderer,
        ICurriculumDayService days,
        IBlockCompiler blocks,
        DocxLessonParser parser,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _renderer = renderer;
        _days = days;
        _blocks = blocks;
        _parser = parser;
        _context = context;
        _userManager = userManager;
    }

    // -------- Day admin (DB-backed) --------

    [HttpGet("Curriculum/Lessons")]
    public async Task<IActionResult> Lessons(int? unitId = null, GradeBand? gradeBand = null)
    {
        var days = await _days.ListAsync(unitId, gradeBand, includeInactive: true);
        var units = await GetUnitOptionsAsync();

        var vm = new CurriculumDayListViewModel
        {
            Days = days.Select(d => new CurriculumDayListItemViewModel
            {
                Id = d.Id,
                Title = d.Title,
                UnitTitle = d.Unit?.Title ?? "—",
                DayNumberInUnit = d.DayNumberInUnit,
                GradeBand = d.GradeBand,
                Theme = d.Theme,
                EstimatedDurationMinutes = d.EstimatedDurationMinutes,
                IsActive = d.IsActive,
                LastModifiedAt = d.LastModifiedAt,
                CreatedAt = d.CreatedAt
            }).ToList(),
            UnitFilter = unitId,
            GradeBandFilter = gradeBand,
            AvailableUnits = units
        };

        return View("Lessons", vm);
    }

    // -------- Docx upload pipeline --------

    [HttpGet("Curriculum/Upload")]
    public async Task<IActionResult> Upload()
    {
        var units = await GetUnitOptionsAsync();
        if (units.Count == 0)
        {
            TempData["ErrorMessage"] = "No Units exist yet. Seed a LearningPath + Unit first.";
            return RedirectToAction(nameof(Lessons));
        }
        return View(new CurriculumUploadViewModel { AvailableUnits = units });
    }

    [HttpPost("Curriculum/Upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(CurriculumUploadViewModel model, CancellationToken ct)
    {
        model.AvailableUnits = await GetUnitOptionsAsync();

        if (model.DocxFile is null || model.DocxFile.Length == 0)
        {
            ModelState.AddModelError(nameof(model.DocxFile), "Please choose a .docx file.");
            return View(model);
        }
        if (!model.DocxFile.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.DocxFile), "File must be a .docx.");
            return View(model);
        }

        ParsedLesson parsed;
        try
        {
            using var stream = model.DocxFile.OpenReadStream();
            parsed = _parser.Parse(stream);
        }
        catch (DocxParseException ex)
        {
            model.ErrorMessage = $"Couldn't parse the document: {ex.Message}";
            return View(model);
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        var gradeBand = ParseGradeBand(parsed.GradeBand);

        // Catch the most common author mistake before we hit the DB: copying
        // a sample template, changing Unit, but forgetting to change Title.
        // The Slug unique index would reject the INSERT with a generic
        // DbUpdateException, leaving the author staring at a 500 with no
        // hint about what to fix. Pre-check the slug so we can give them a
        // direct message instead.
        var candidateSlug = Slugify(parsed.Title);
        var slugClash = await _context.Days
            .Where(d => d.Slug == candidateSlug)
            .Select(d => new { d.Id, d.Title })
            .FirstOrDefaultAsync(ct);
        if (slugClash is not null)
        {
            model.ErrorMessage =
                $"A lesson titled \"{slugClash.Title}\" already exists. " +
                $"Open your .docx, change the Title row in the Metadata table to the new lesson's name " +
                $"(not the sample's), save, and upload again.";
            return View(model);
        }

        // Resolve the parent Unit. Order of preference:
        //   1. Explicit dropdown selection (admin override).
        //   2. Case-insensitive title match against existing Units.
        //   3. Auto-create a Unit with the parsed name, under the first
        //      LearningPath whose grade band matches (or the first path
        //      overall if no band match).
        // Auto-creation means Karen never has to pre-seed Units to upload
        // a new lesson — typos won't block her, though they will create
        // a duplicate Unit she can rename later.
        int unitId;
        if (model.UnitId is int explicitChoice)
        {
            unitId = explicitChoice;
        }
        else
        {
            var match = await _context.Units.FirstOrDefaultAsync(
                u => u.Title.ToLower() == parsed.Unit.ToLower(), ct);
            if (match is not null)
            {
                unitId = match.Id;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(parsed.Unit))
                {
                    model.ErrorMessage = "The Metadata table is missing a Unit name.";
                    return View(model);
                }
                var path = await _context.LearningPaths
                               .Where(p => p.IsActive && p.GradeBand == gradeBand)
                               .FirstOrDefaultAsync(ct)
                           ?? await _context.LearningPaths
                               .Where(p => p.IsActive)
                               .OrderBy(p => p.Id)
                               .FirstOrDefaultAsync(ct);
                if (path is null)
                {
                    model.ErrorMessage = "No active LearningPath exists. Seed at least one before uploading lessons.";
                    return View(model);
                }
                var nextUnitNumber = await _context.Units
                    .Where(u => u.LearningPathId == path.Id)
                    .Select(u => (int?)u.UnitNumber)
                    .MaxAsync(ct) ?? 0;
                var newUnit = new Unit
                {
                    Title = parsed.Unit.Trim(),
                    UnitNumber = nextUnitNumber + 1,
                    LearningPathId = path.Id,
                    Theme = parsed.Theme,
                    IsActive = true
                };
                _context.Units.Add(newUnit);
                await _context.SaveChangesAsync(ct);
                unitId = newUnit.Id;
                TempData["InfoMessage"] = $"Created new Unit \"{newUnit.Title}\" under LearningPath \"{path.Title}\".";
            }
        }

        // Body sections compiled into one Markdown blob inside a single Raw
        // block. Karen edits in the existing block editor afterward — the
        // parser doesn't pre-decide how the lesson should be sliced.
        var bodyMarkdown = CompileBodyMarkdown(parsed);
        var rawBlock = new RawMarkdownBlock { Markdown = bodyMarkdown };
        var bodyBlocksJson = _blocks.Serialize(new List<Block> { rawBlock });

        var day = new Day
        {
            Title = parsed.Title,
            Description = parsed.Objective,
            UnitId = unitId,
            DayNumberInUnit = 1,
            GradeBand = gradeBand,
            Theme = parsed.Theme,
            EstimatedDurationMinutes = parsed.DurationMinutes,
            Sessions = parsed.Sessions,
            Slug = Slugify(parsed.Title),
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                vocab = new { core = parsed.VocabCore, stretch = parsed.VocabStretch, challenge = parsed.VocabChallenge },
                materials = parsed.Materials,
                trimester = parsed.Trimester,
                objective = parsed.Objective,
                standardCodes = parsed.StandardCodes,
                // Body sections kept individually so the proposal renderer can
                // weave songs / callouts / standards into each section rather
                // than dumping one long Markdown blob.
                bodySections = new
                {
                    apertura     = parsed.BodySections.GetValueOrDefault(LessonSection.Apertura, ""),
                    presentacion = parsed.BodySections.GetValueOrDefault(LessonSection.Presentacion, ""),
                    extension    = parsed.BodySections.GetValueOrDefault(LessonSection.Extension, ""),
                    actividad    = parsed.BodySections.GetValueOrDefault(LessonSection.Actividad, ""),
                    juegos       = parsed.BodySections.GetValueOrDefault(LessonSection.Juegos, ""),
                    cultura      = parsed.BodySections.GetValueOrDefault(LessonSection.Cultura, ""),
                    cierre       = parsed.BodySections.GetValueOrDefault(LessonSection.Cierre, "")
                }
            }),
            BodyBlocksJson = bodyBlocksJson,
            TeacherPlanMarkdown = bodyMarkdown,
            IsActive = false,
            IsPublic = false
        };

        await _days.CreateAsync(day, userId, $"Uploaded from {model.DocxFile.FileName}");

        // Songs → LessonVideos
        foreach (var song in parsed.Songs)
        {
            _context.LessonVideos.Add(new LessonVideo
            {
                DayId = day.Id,
                Title = song.Title,
                Url = song.Url,
                Role = song.Role,
                DisplayOrder = song.DisplayOrder
            });
        }

        // Standards → attach by code
        if (parsed.StandardCodes.Count > 0)
        {
            var standards = await _context.WisconsinStandards
                .Where(s => parsed.StandardCodes.Contains(s.Code))
                .ToListAsync(ct);
            var tracked = await _context.Days
                .Include(d => d.WisconsinStandards)
                .FirstAsync(d => d.Id == day.Id, ct);
            foreach (var s in standards) tracked.WisconsinStandards.Add(s);
        }

        // Allocate a shortlink for the Day.
        var code = await GenerateUniqueShortlinkCodeAsync(ct);
        _context.Shortlinks.Add(new Shortlink
        {
            Code = code,
            DestinationType = ShortlinkDestination.Lesson,
            DestinationId = day.Id
        });

        await _context.SaveChangesAsync(ct);

        TempData["SuccessMessage"] = $"Uploaded \"{day.Title}\". Shortlink: lcs/{code}. Review below.";
        return RedirectToAction(nameof(ReviewDay), new { id = day.Id });
    }

    // -------- helpers (docx upload) --------

    private static string CompileBodyMarkdown(ParsedLesson p)
    {
        var sb = new System.Text.StringBuilder();
        void AppendIfPresent(LessonSection section, string heading)
        {
            if (!p.BodySections.TryGetValue(section, out var text) || string.IsNullOrWhiteSpace(text)) return;
            sb.AppendLine($"## {heading}");
            sb.AppendLine();
            sb.AppendLine(text);
            sb.AppendLine();
        }
        AppendIfPresent(LessonSection.Apertura,    "Apertura");
        AppendIfPresent(LessonSection.Presentacion, "Presentación");
        AppendIfPresent(LessonSection.Extension,   "Extensión");
        AppendIfPresent(LessonSection.Actividad,   "Actividad");
        AppendIfPresent(LessonSection.Juegos,      "Opciones de juegos");
        AppendIfPresent(LessonSection.Cultura,     "Cultura");
        AppendIfPresent(LessonSection.Cierre,      "Cierre");
        return sb.ToString().Trim();
    }

    private static GradeBand ParseGradeBand(string text)
    {
        // Accept enum name ("K5", "Grade3") OR the human band form Karen uses
        // ("K–2", "3-5"). Bands map to the lowest grade in the range; admin can
        // adjust in the Day editor after upload.
        var normalized = text.Trim().Replace("–", "-").Replace(" ", "");
        if (Enum.TryParse<GradeBand>(normalized, true, out var direct)) return direct;
        return normalized.ToUpperInvariant() switch
        {
            "K-2" or "K2"     => GradeBand.K5,
            "3-5" or "GR3-5"  => GradeBand.Grade3,
            "6-8" or "GR6-8"  => GradeBand.Grade6,
            "K"               => GradeBand.K5,
            _                 => GradeBand.K5
        };
    }

    private static string Slugify(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_') sb.Append('-');
            // strip diacritics roughly
            else if ("áàäâ".Contains(c)) sb.Append('a');
            else if ("éèëê".Contains(c)) sb.Append('e');
            else if ("íìïî".Contains(c)) sb.Append('i');
            else if ("óòöô".Contains(c)) sb.Append('o');
            else if ("úùüû".Contains(c)) sb.Append('u');
            else if (c == 'ñ') sb.Append('n');
        }
        var result = sb.ToString();
        while (result.Contains("--")) result = result.Replace("--", "-");
        return result.Trim('-');
    }

    private async Task<string> GenerateUniqueShortlinkCodeAsync(CancellationToken ct)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // skip I/O/0/1 ambiguity
        var rng = Random.Shared;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var code = new string(Enumerable.Range(0, 3).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
            var taken = await _context.Shortlinks.AnyAsync(s => s.Code == code, ct);
            if (!taken) return code;
        }
        throw new InvalidOperationException("Couldn't allocate a unique shortlink code after 12 attempts.");
    }

    /// <summary>
    /// Read-only rendered review of a Day. The primary view of a hand-authored
    /// lesson — uses the same render pipeline the binder uses, so what the
    /// reviewer sees is what teachers will print.
    /// </summary>
    [HttpGet("Curriculum/Lessons/{id:int}/Review")]
    public async Task<IActionResult> ReviewDay(int id)
    {
        var day = await _context.Days
            .Include(d => d.Unit)
            .Include(d => d.WisconsinStandards)
            .Include(d => d.Videos)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (day is null) return NotFound();

        var print = LessonPrintViewModelBuilder.Build(day);
        var shortlinkCode = await _context.Shortlinks
            .Where(s => s.DestinationType == ShortlinkDestination.Lesson && s.DestinationId == day.Id)
            .Select(s => s.Code)
            .FirstOrDefaultAsync();

        return View(new CurriculumDayReviewViewModel
        {
            DayId = day.Id,
            Title = day.Title,
            Theme = day.Theme,
            UnitTitle = day.Unit?.Title ?? "—",
            GradeBand = day.GradeBand,
            EstimatedDurationMinutes = day.EstimatedDurationMinutes,
            IsActive = day.IsActive,
            WiStandardCodes = day.WisconsinStandards.Select(s => s.Code).OrderBy(c => c).ToList(),
            RenderedHtml = string.Empty, // legacy; ReviewDay now uses Print
            Print = print,
            ShortlinkCode = shortlinkCode
        });
    }

    // Print VM assembly moved to Services/Curriculum/LessonPrintViewModelBuilder
    // so PublicCurriculumController can reuse it without injecting this controller.

    /// <summary>
    /// Same rendered lesson as Review, but with no admin chrome — suitable for
    /// Ctrl+P / Save as PDF. Identical render path so what teachers see in the
    /// binder is byte-identical to what the reviewer signs off on.
    /// </summary>
    [HttpGet("Curriculum/Lessons/{id:int}/Print")]
    public async Task<IActionResult> PrintDay(int id)
    {
        var day = await _context.Days
            .Include(d => d.Unit)
            .Include(d => d.WisconsinStandards)
            .Include(d => d.Videos)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (day is null) return NotFound();
        return View(LessonPrintViewModelBuilder.Build(day));
    }

    /// <summary>
    /// Flip a Day's IsActive flag in either direction. Activating publishes the
    /// lesson to the public shortlink + curriculum routes; deactivating pulls it
    /// back to draft so it stays in the admin list but 404s from public view.
    /// </summary>
    [HttpPost("Curriculum/Lessons/{id:int}/Toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, string? returnTo, CancellationToken ct)
    {
        var day = await _days.GetAsync(id, ct);
        if (day is null) return NotFound();

        day.IsActive = !day.IsActive;
        var userId = _userManager.GetUserId(User) ?? string.Empty;
        var note = day.IsActive ? "Activated by reviewer." : "Deactivated by reviewer.";
        await _days.UpdateAsync(day, userId, note, ct);

        TempData["SuccessMessage"] = day.IsActive
            ? $"\"{day.Title}\" is active."
            : $"\"{day.Title}\" moved back to draft.";

        return string.Equals(returnTo, "list", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Lessons))
            : RedirectToAction(nameof(ReviewDay), new { id });
    }

    // -------- Power-user mode: metadata-only Create + block editor on Edit --------

    [HttpGet("Curriculum/Lessons/Create")]
    public async Task<IActionResult> CreateDay()
    {
        var units = await GetUnitOptionsAsync();
        if (units.Count == 0)
        {
            TempData["ErrorMessage"] = "No Units exist yet. Seed a LearningPath + Unit first, or build the Units admin to create one.";
            return RedirectToAction(nameof(Lessons));
        }

        return View("DayForm", new CurriculumDayFormViewModel
        {
            UnitId = units[0].Id,
            AvailableUnits = units
        });
    }

    [HttpPost("Curriculum/Lessons/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDay(CurriculumDayFormViewModel model)
    {
        model.AvailableUnits = await GetUnitOptionsAsync();
        if (!ModelState.IsValid) return View("DayForm", model);

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        var day = ApplyMetadataToEntity(model, new Day());
        // New Days start with an empty block list. Karen adds blocks in edit mode
        // via the HTMX block editor; the compiled TeacherPlanMarkdown stays empty
        // until the first block is saved.
        day.BodyBlocksJson = string.Empty;
        day.TeacherPlanMarkdown = string.Empty;
        await _days.CreateAsync(day, userId, model.ChangeNotes);

        TempData["SuccessMessage"] = $"Created \"{day.Title}\". Add lesson content below.";
        return RedirectToAction(nameof(EditDay), new { id = day.Id });
    }

    [HttpGet("Curriculum/Lessons/Edit/{id:int}")]
    public async Task<IActionResult> EditDay(int id)
    {
        var day = await _days.GetAsync(id);
        if (day is null) return NotFound();

        var vm = new CurriculumDayFormViewModel
        {
            Id = day.Id,
            Title = day.Title,
            Description = day.Description,
            UnitId = day.UnitId,
            DayNumberInUnit = day.DayNumberInUnit,
            GradeBand = day.GradeBand,
            Theme = day.Theme,
            EstimatedDurationMinutes = day.EstimatedDurationMinutes,
            SkillFocus = day.SkillFocus,
            TeacherPlanMarkdown = day.TeacherPlanMarkdown,
            BodyBlocksJson = day.BodyBlocksJson,
            IsActive = day.IsActive,
            AvailableUnits = await GetUnitOptionsAsync()
        };
        return View("DayForm", vm);
    }

    [HttpPost("Curriculum/Lessons/Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDay(int id, CurriculumDayFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        model.AvailableUnits = await GetUnitOptionsAsync();
        if (!ModelState.IsValid)
        {
            // Re-hydrate the block JSON so the editor still renders on validation failure.
            var current = await _days.GetAsync(id);
            if (current is not null) model.BodyBlocksJson = current.BodyBlocksJson;
            return View("DayForm", model);
        }

        var existing = await _days.GetAsync(id);
        if (existing is null) return NotFound();

        var userId = _userManager.GetUserId(User) ?? string.Empty;
        ApplyMetadataToEntity(model, existing);
        await _days.UpdateAsync(existing, userId, model.ChangeNotes);

        TempData["SuccessMessage"] = $"Saved metadata for \"{existing.Title}\".";
        return RedirectToAction(nameof(EditDay), new { id = existing.Id });
    }

    /// <summary>
    /// Live-preview endpoint for the authoring UI: accepts the current Markdown
    /// from the editor textarea (form-urlencoded) and returns just the rendered
    /// HTML fragment for the preview pane. No DB write.
    /// </summary>
    [HttpPost("Curriculum/Lessons/Preview")]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewDay([FromForm] string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Content("<p style='color:#6B7280; padding:1em;'>Enter Markdown to see a preview.</p>", "text/html");
        }

        var rendered = _renderer.Render(markdown);
        // Wrap in .lcs-document so the theme CSS applies. The CSS link is in
        // the parent page, not this fragment.
        var html = $"<div class=\"lcs-document\">{rendered.BodyHtml}</div>";
        return Content(html, "text/html");
    }

    /// <summary>
    /// Renders the Markdown compiled from a Day's current block list — used by
    /// the live preview pane on the block editor form. Reads the saved blocks
    /// from the Day row; the form sends an in-flight save through SaveBlock
    /// endpoints before requesting a preview refresh.
    /// </summary>
    [HttpGet("Curriculum/Lessons/{dayId:int}/Preview")]
    public async Task<IActionResult> PreviewDayBlocks(int dayId)
    {
        var day = await _days.GetAsync(dayId);
        if (day is null) return NotFound();
        var blocks = _blocks.Deserialize(day.BodyBlocksJson);
        var markdown = blocks.Count > 0 ? _blocks.Compile(blocks) : day.TeacherPlanMarkdown;
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Content("<p style='color:#6B7280; padding:1em;'>Add some blocks to see a preview.</p>", "text/html");
        }
        var rendered = _renderer.Render(markdown);
        return Content($"<div class=\"lcs-document\">{rendered.BodyHtml}</div>", "text/html");
    }

    // -------- Block editor HTMX endpoints --------

    /// <summary>Re-render a single block in view mode — used as the Cancel target.</summary>
    [HttpGet("Curriculum/Lessons/{dayId:int}/Blocks/{blockId}")]
    public async Task<IActionResult> ViewBlock(int dayId, string blockId)
    {
        var day = await _days.GetAsync(dayId);
        if (day is null) return NotFound();
        var blocks = _blocks.Deserialize(day.BodyBlocksJson).ToList();
        var block = blocks.FirstOrDefault(b => b.Id == blockId);
        if (block is null) return NotFound();
        return PartialView("Blocks/_BlockItem", new BlockItemViewModel { DayId = dayId, Block = block });
    }

    [HttpGet("Curriculum/Lessons/{dayId:int}/Blocks/{blockId}/Edit")]
    public async Task<IActionResult> EditBlock(int dayId, string blockId)
    {
        var day = await _days.GetAsync(dayId);
        if (day is null) return NotFound();
        var blocks = _blocks.Deserialize(day.BodyBlocksJson).ToList();
        var block = blocks.FirstOrDefault(b => b.Id == blockId);
        if (block is null) return NotFound();
        return PartialView("Blocks/_BlockItem", new BlockItemViewModel { DayId = dayId, Block = block, IsEditing = true });
    }

    /// <summary>Appends a new block of the requested kind. Returns the rendered item fragment.</summary>
    [HttpPost("Curriculum/Lessons/{dayId:int}/Blocks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBlock(int dayId, [FromForm] string kind)
    {
        var day = await _days.GetAsync(dayId);
        if (day is null) return NotFound();

        Block fresh = kind?.ToLowerInvariant() switch
        {
            "vocab" => new VocabBlock { Rows = new List<VocabRow> { new(), new(), new() } },
            "raw"   => new RawMarkdownBlock { Markdown = string.Empty },
            _       => new ParagraphBlock { ContainerKind = "warmup", Time = "3 min", Text = string.Empty }
        };

        var blocks = _blocks.Deserialize(day.BodyBlocksJson).ToList();
        blocks.Add(fresh);
        await PersistBlocksAsync(day, blocks);

        // Open the new block in edit mode immediately so the author starts typing.
        return PartialView("Blocks/_BlockItem", new BlockItemViewModel { DayId = dayId, Block = fresh, IsEditing = true });
    }

    /// <summary>
    /// Persists edits to a single block. Form data is shape-specific —
    /// untyped form keys arrive as IFormCollection and are dispatched on the
    /// existing block's runtime type.
    /// </summary>
    [HttpPost("Curriculum/Lessons/{dayId:int}/Blocks/{blockId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBlock(int dayId, string blockId, [FromForm] IFormCollection form)
    {
        var day = await _days.GetAsync(dayId);
        if (day is null) return NotFound();

        var blocks = _blocks.Deserialize(day.BodyBlocksJson).ToList();
        var idx = blocks.FindIndex(b => b.Id == blockId);
        if (idx < 0) return NotFound();

        var existing = blocks[idx];
        Block updated = existing switch
        {
            ParagraphBlock     => UpdateParagraph((ParagraphBlock)existing, form),
            VocabBlock         => UpdateVocab((VocabBlock)existing, form),
            RawMarkdownBlock   => UpdateRaw((RawMarkdownBlock)existing, form),
            _                  => existing
        };
        blocks[idx] = updated;

        await PersistBlocksAsync(day, blocks);

        return PartialView("Blocks/_BlockItem", new BlockItemViewModel { DayId = dayId, Block = updated });
    }

    [HttpDelete("Curriculum/Lessons/{dayId:int}/Blocks/{blockId}")]
    public async Task<IActionResult> DeleteBlock(int dayId, string blockId)
    {
        var day = await _days.GetAsync(dayId);
        if (day is null) return NotFound();

        var blocks = _blocks.Deserialize(day.BodyBlocksJson).ToList();
        blocks.RemoveAll(b => b.Id == blockId);
        await PersistBlocksAsync(day, blocks);

        // Empty body tells HTMX to swap the block out without replacing it.
        return Content(string.Empty, "text/html");
    }

    // -------- block mutation helpers --------

    private static ParagraphBlock UpdateParagraph(ParagraphBlock p, IFormCollection form)
    {
        p.Text = form["text"].ToString() ?? string.Empty;
        var kind = form["containerKind"].ToString();
        p.ContainerKind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        var time = form["time"].ToString();
        p.Time = string.IsNullOrWhiteSpace(time) ? null : time.Trim();
        return p;
    }

    private static VocabBlock UpdateVocab(VocabBlock v, IFormCollection form)
    {
        var rows = new List<VocabRow>();
        var i = 0;
        while (true)
        {
            var prefix = $"rows[{i}].";
            if (!form.ContainsKey(prefix + "Spanish") &&
                !form.ContainsKey(prefix + "English") &&
                !form.ContainsKey(prefix + "Pronunciation") &&
                !form.ContainsKey(prefix + "Cue"))
            {
                break;
            }

            var row = new VocabRow
            {
                Spanish = form[prefix + "Spanish"].ToString()?.Trim() ?? string.Empty,
                English = form[prefix + "English"].ToString()?.Trim() ?? string.Empty,
                Pronunciation = form[prefix + "Pronunciation"].ToString()?.Trim() ?? string.Empty,
                Cue = form[prefix + "Cue"].ToString()?.Trim() ?? string.Empty
            };
            // Drop empty rows so authors can leave trailing blanks without polluting the table.
            if (!string.IsNullOrEmpty(row.Spanish) || !string.IsNullOrEmpty(row.English))
            {
                rows.Add(row);
            }
            i++;
        }
        v.Rows = rows;
        return v;
    }

    private static RawMarkdownBlock UpdateRaw(RawMarkdownBlock r, IFormCollection form)
    {
        r.Markdown = form["markdown"].ToString() ?? string.Empty;
        return r;
    }

    /// <summary>
    /// Stores the block list as JSON and recompiles TeacherPlanMarkdown so the
    /// downstream renderer / preview pipeline keeps working unchanged.
    /// Also runs the version-snapshot side effect via the Day service.
    /// </summary>
    private async Task PersistBlocksAsync(Day day, List<Block> blocks)
    {
        day.BodyBlocksJson = _blocks.Serialize(blocks);
        day.TeacherPlanMarkdown = _blocks.Compile(blocks);
        var userId = _userManager.GetUserId(User) ?? string.Empty;
        await _days.UpdateAsync(day, userId, changeNotes: null);
    }

    // -------- helpers --------

    private async Task<IReadOnlyList<UnitOption>> GetUnitOptionsAsync() =>
        await _context.Units
            .Where(u => u.IsActive)
            .OrderBy(u => u.UnitNumber)
            .Select(u => new UnitOption(u.Id, u.Title))
            .ToListAsync();

    /// <summary>
    /// Copies metadata fields from the form to the Day entity. Does NOT touch
    /// <see cref="Day.TeacherPlanMarkdown"/> or <see cref="Day.BodyBlocksJson"/> —
    /// those are written exclusively by the HTMX block-editor endpoints to
    /// avoid race conditions where a stale form value clobbers fresh block edits.
    /// </summary>
    private static Day ApplyMetadataToEntity(CurriculumDayFormViewModel m, Day d)
    {
        d.Id = m.Id;
        d.Title = m.Title;
        d.Description = m.Description ?? string.Empty;
        d.UnitId = m.UnitId;
        d.DayNumberInUnit = m.DayNumberInUnit;
        d.GradeBand = m.GradeBand;
        d.Theme = m.Theme ?? string.Empty;
        d.EstimatedDurationMinutes = m.EstimatedDurationMinutes;
        d.SkillFocus = m.SkillFocus;
        d.IsActive = m.IsActive;
        return d;
    }

}
