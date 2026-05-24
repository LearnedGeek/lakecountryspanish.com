using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using LakeCountrySpanish.Web.Services.Curriculum;
using LakeCountrySpanish.Web.Services.Curriculum.Blocks;
using LakeCountrySpanish.Web.Services.Curriculum.Drafter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class CurriculumController : Controller
{
    private readonly IDocumentRenderingService _renderer;
    private readonly ICurriculumDayService _days;
    private readonly IBlockCompiler _blocks;
    private readonly ICurriculumDrafter _drafter;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public CurriculumController(
        IDocumentRenderingService renderer,
        ICurriculumDayService days,
        IBlockCompiler blocks,
        ICurriculumDrafter drafter,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment)
    {
        _renderer = renderer;
        _days = days;
        _blocks = blocks;
        _drafter = drafter;
        _context = context;
        _userManager = userManager;
        _environment = environment;
    }

    // -------- Sample-file preview routes (dev only; backed by docs/curriculum-system/samples) --------

    public IActionResult PreviewSample()
    {
        var markdown = ReadSampleOrNull("los-colores.md");
        if (markdown is null) return NotFound("Sample doc los-colores.md not found.");

        return View(_renderer.Render(markdown));
    }

    public IActionResult PreviewBinder(string slug = "los-colores")
    {
        var lessonMd = ReadSampleOrNull($"{slug}.md");
        if (lessonMd is null) return NotFound($"Lesson {slug}.md not found.");

        var lesson = _renderer.Render(lessonMd);

        var artifacts = new List<RenderedDocument>();
        foreach (var reference in lesson.Frontmatter.Artifacts)
        {
            if (string.IsNullOrWhiteSpace(reference.Slug)) continue;
            var artifactMd = ReadSampleOrNull($"{reference.Slug}.md");
            if (artifactMd is null) continue;
            artifacts.Add(_renderer.Render(artifactMd));
        }

        return View(new CurriculumBinderViewModel
        {
            Lesson = lesson,
            Artifacts = artifacts,
            TeacherName = User.Identity?.Name ?? "Teacher",
            GeneratedAt = DateTime.UtcNow
        });
    }

    // -------- Day admin (DB-backed) --------

    [HttpGet("Curriculum/Days")]
    public async Task<IActionResult> Days(int? unitId = null, GradeBand? gradeBand = null)
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

        return View(vm);
    }

    // -------- AI-assisted drafter (Karen's primary path) --------

    /// <summary>
    /// Brief form: a single textarea where Karen describes the lesson she wants.
    /// Per docs/curriculum-system/ai-assisted-authoring.md this is the default
    /// "+ New Day" experience; the existing block editor is the power-user mode.
    /// </summary>
    [HttpGet("Curriculum/Days/New")]
    public async Task<IActionResult> NewDay()
    {
        var units = await GetUnitOptionsAsync();
        if (units.Count == 0)
        {
            TempData["ErrorMessage"] = "No Units exist yet. A LearningPath + Unit must be seeded before drafting lessons.";
            return RedirectToAction(nameof(Days));
        }

        return View("Draft", new CurriculumDraftBriefViewModel
        {
            AvailableUnits = units,
            DrafterAvailable = _drafter.IsAvailable
        });
    }

    [HttpPost("Curriculum/Days/New")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewDay(CurriculumDraftBriefViewModel model, CancellationToken ct)
    {
        model.AvailableUnits = await GetUnitOptionsAsync();
        model.DrafterAvailable = _drafter.IsAvailable;
        if (!ModelState.IsValid) return View("Draft", model);

        if (!_drafter.IsAvailable)
        {
            model.ErrorMessage = "AI drafting is unavailable — the Claude API key isn't configured.";
            return View("Draft", model);
        }

        var result = await _drafter.DraftAsync(new DraftLessonRequest
        {
            Brief = model.Brief,
            UnitId = model.UnitId,
            GradeBand = model.GradeBand
        }, ct);

        if (!result.Success)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Drafting failed for an unknown reason.";
            return View("Draft", model);
        }

        var userId = _userManager.GetUserId(User) ?? string.Empty;

        var day = new Day
        {
            Title = result.Title,
            Description = string.Empty,
            UnitId = result.UnitId > 0 ? result.UnitId : (model.UnitId ?? model.AvailableUnits.First().Id),
            DayNumberInUnit = 1,
            GradeBand = result.GradeBand,
            Theme = result.Theme,
            EstimatedDurationMinutes = result.EstimatedDurationMinutes,
            SkillFocus = SkillFocus.Mixed,
            BodyBlocksJson = _blocks.Serialize(result.Blocks),
            TeacherPlanMarkdown = _blocks.Compile(result.Blocks),
            IsActive = false       // Drafts land inactive; Karen approves on the Review page.
        };

        await _days.CreateAsync(day, userId, "AI drafted from brief.");

        // Attach inferred WI standards by code.
        if (result.WiStandardCodes.Count > 0)
        {
            var matched = await _context.WisconsinStandards
                .Where(s => result.WiStandardCodes.Contains(s.Code))
                .ToListAsync(ct);
            var tracked = await _context.Days.Include(d => d.WisconsinStandards).FirstAsync(d => d.Id == day.Id, ct);
            foreach (var std in matched) tracked.WisconsinStandards.Add(std);
            await _context.SaveChangesAsync(ct);
        }

        TempData["SuccessMessage"] = $"Drafted \"{day.Title}\". Review below.";
        return RedirectToAction(nameof(ReviewDay), new { id = day.Id });
    }

    /// <summary>
    /// Read-only rendered review of a Day. Karen's primary view of a drafted
    /// (or hand-authored) lesson — uses the same render pipeline the binder
    /// uses, so what she sees is what teachers will print.
    /// </summary>
    [HttpGet("Curriculum/Days/{id:int}/Review")]
    public async Task<IActionResult> ReviewDay(int id)
    {
        var day = await _context.Days
            .Include(d => d.Unit)
            .Include(d => d.WisconsinStandards)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (day is null) return NotFound();

        var markdown = day.TeacherPlanMarkdown;
        if (string.IsNullOrWhiteSpace(markdown))
        {
            var blocks = _blocks.Deserialize(day.BodyBlocksJson);
            markdown = _blocks.Compile(blocks);
        }

        var rendered = string.IsNullOrWhiteSpace(markdown)
            ? "<p style='color:#6B7280;padding:1em;'>This Day has no content yet.</p>"
            : _renderer.Render(markdown).BodyHtml;

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
            RenderedHtml = rendered
        });
    }

    /// <summary>
    /// Flip a drafted Day to active so it shows up in the Days list as available
    /// for binder composition. Idempotent.
    /// </summary>
    [HttpPost("Curriculum/Days/{id:int}/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDay(int id, CancellationToken ct)
    {
        var day = await _days.GetAsync(id, ct);
        if (day is null) return NotFound();
        if (!day.IsActive)
        {
            day.IsActive = true;
            var userId = _userManager.GetUserId(User) ?? string.Empty;
            await _days.UpdateAsync(day, userId, "Activated by reviewer.", ct);
        }
        TempData["SuccessMessage"] = $"\"{day.Title}\" is active.";
        return RedirectToAction(nameof(ReviewDay), new { id });
    }

    // -------- Power-user mode: metadata-only Create + block editor on Edit --------

    [HttpGet("Curriculum/Days/Create")]
    public async Task<IActionResult> CreateDay()
    {
        var units = await GetUnitOptionsAsync();
        if (units.Count == 0)
        {
            TempData["ErrorMessage"] = "No Units exist yet. Seed a LearningPath + Unit first, or build the Units admin to create one.";
            return RedirectToAction(nameof(Days));
        }

        return View("DayForm", new CurriculumDayFormViewModel
        {
            UnitId = units[0].Id,
            AvailableUnits = units
        });
    }

    [HttpPost("Curriculum/Days/Create")]
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

    [HttpGet("Curriculum/Days/Edit/{id:int}")]
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

    [HttpPost("Curriculum/Days/Edit/{id:int}")]
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
    [HttpPost("Curriculum/Days/Preview")]
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
    [HttpGet("Curriculum/Days/{dayId:int}/Preview")]
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
    [HttpGet("Curriculum/Days/{dayId:int}/Blocks/{blockId}")]
    public async Task<IActionResult> ViewBlock(int dayId, string blockId)
    {
        var day = await _days.GetAsync(dayId);
        if (day is null) return NotFound();
        var blocks = _blocks.Deserialize(day.BodyBlocksJson).ToList();
        var block = blocks.FirstOrDefault(b => b.Id == blockId);
        if (block is null) return NotFound();
        return PartialView("Blocks/_BlockItem", new BlockItemViewModel { DayId = dayId, Block = block });
    }

    [HttpGet("Curriculum/Days/{dayId:int}/Blocks/{blockId}/Edit")]
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
    [HttpPost("Curriculum/Days/{dayId:int}/Blocks")]
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
    [HttpPost("Curriculum/Days/{dayId:int}/Blocks/{blockId}")]
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

    [HttpDelete("Curriculum/Days/{dayId:int}/Blocks/{blockId}")]
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

    private string? ReadSampleOrNull(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath, "..", "..",
            "docs", "curriculum-system", "samples", fileName));
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
    }

}
