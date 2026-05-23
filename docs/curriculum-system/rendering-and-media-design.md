# LCS Document Rendering and Media Library — Design

**Status:** Design draft, 2026-05-23
**Author:** Mark, with Claude
**Reference implementation:** Ellis Hope Foundation media library pattern (`E:\Documents\Work\dev\repos\EHF\EllisHopeFoundation\EllisHope`)
**Audience:** Mark, as the developer who'll build this; future Claude sessions resuming the work

This document defines the rendering and media subsystem that lets Karen author K4-8 Spanish curriculum (lessons, worksheets, games, flashcards, cultural materials) inside the LCS web platform, and lets teachers generate watermarked PDF binders on demand. It is part of Phase 3 of the September 2026 launch plan.

---

## Problem statement

The LearnedGeek proposal generator is the wrong tool for this work for three reasons:

1. **It's a developer console tool.** Karen and the founding instructors won't `cd` into a directory and run `dotnet run -- some-lesson.md`. They'll click "Generate Binder" in their teacher dashboard. The renderer has to live inside the LCS web app.

2. **The renderer needs platform data.** `Day`, `ArtifactLibrary` items, `TeacherClassAssignment` for watermarking, `BinderComposition` for ad-hoc teacher selection. Shuttling EF Core entities into temp Markdown files and back is brittle. Native rendering inside the .NET process is simpler.

3. **The aesthetic and authoring lifecycle differ.** Proposals are one-off business documents authored in VS Code. K4-8 worksheets are recurring, content-managed, authored by teachers in a web form, with versioning, attribution to media sources, and teacher-watermarked print output.

The LCS rendering subsystem replaces the proposal-generator path for all in-platform document needs. The proposal generator remains as Mark's tool for LearnedGeek client deliverables; LCS rendering is its own thing.

---

## High-level architecture

Four service layers + an in-platform Markdown rendering pipeline:

```
┌──────────────────────────────────────────────────────────────────────┐
│ TeacherController / AdminController / API endpoints                  │
│   (entry points: "Generate Binder", "Render Preview", "Upload",      │
│    "Browse Pixabay", "Import to Library", etc.)                      │
└────────────────────┬─────────────────────────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
┌───────────────┐ ┌──────────────┐ ┌─────────────────────────────────┐
│ IDocument     │ │ IMediaService│ │ IImageSourceService             │
│ RenderingSvc  │ │  (CRUD on    │ │  (adapter: Pixabay, Unsplash,   │
│  (Razor +     │ │   MediaAsset │ │   AIGenerated; pluggable)       │
│   Markdig +   │ │   + Usage)   │ └─────────────────────────────────┘
│   Puppeteer-  │ └──────┬───────┘                  │
│   Sharp)      │        │                          ▼
└──────┬────────┘        │              ┌─────────────────────────┐
       │                 │              │ IImageProcessingService │
       │                 │              │  (SixLabors.ImageSharp: │
       │                 │              │   resize, thumbnail,    │
       │                 │              │   optimize, hash)       │
       │                 │              └─────────────────────────┘
       │                 ▼
       │      ┌───────────────────────┐
       │      │ MediaAsset, MediaUsage│
       │      │ (EF Core entities)    │
       │      └───────────────────────┘
       │
       ▼
┌────────────────────────────────────────────┐
│ Markdig pipeline with LCS custom container │
│ blocks: :::trace, :::bingo-card, :::vocab, │
│ :::flashcards, :::color-target, etc.       │
└────────────────────────────────────────────┘
```

---

## Data model

### MediaAsset

Borrowed heavily from EHF's `Media` entity, adapted for LCS curriculum.

```csharp
public class MediaAsset
{
    public int Id { get; set; }
    public string FileName { get; set; }                 // original filename
    public string FilePath { get; set; }                 // e.g. /uploads/media/pixabay/123456.jpg
    public MediaSource Source { get; set; }              // Upload | Pixabay | Unsplash | AIGenerated
    public MediaCategory Category { get; set; }          // see enum below
    public string? SourceId { get; set; }                // external ID at source
    public string? SourceUrl { get; set; }               // original URL at source
    public string? Photographer { get; set; }            // attribution: name
    public string? PhotographerUrl { get; set; }         // attribution: profile link
    public string? LicenseType { get; set; }             // "Pixabay License", "Unsplash License", "Public Domain"
    public string? LicenseText { get; set; }             // free-form license note
    public long FileSize { get; set; }
    public string MimeType { get; set; }
    public string? AltText { get; set; }                 // accessibility + screen-reader
    public string? Title { get; set; }
    public string? Tags { get; set; }                    // comma-separated
    public int Width { get; set; }
    public int Height { get; set; }
    public string FileHash { get; set; }                 // SHA256, for dedup
    public string ThumbnailsJson { get; set; }           // JSON dict: { "thumb": "/uploads/.../thumb.jpg", "web": "...", ... }
    public DateTime UploadedDate { get; set; }
    public string UploadedById { get; set; }             // FK to ApplicationUser
    public string? AiPrompt { get; set; }                // if Source=AIGenerated, the prompt used
    public string? AiModel { get; set; }                 // if Source=AIGenerated, e.g. "stable-diffusion-xl"

    public ICollection<MediaUsage> Usages { get; set; }
}

public enum MediaSource
{
    Upload,
    Pixabay,
    Unsplash,
    AIGenerated
}

public enum MediaCategory
{
    Uncategorized,
    Mascot,                   // Lola the llama and variants
    CulturalImage,            // Peru flag, Día de Muertos, foods of Spanish-speaking countries
    FlashcardArt,             // single-object illustrations for vocab cards
    WorksheetIllustration,    // decorations and content art for worksheets
    GameAsset,                // bingo cell icons, memory-match pairs, board game tiles
    Background,               // decorative page backgrounds and borders
    Icon,                     // small functional icons (checkmark, arrow, star)
    Decorative,               // borders, dividers, banners
    Other
}
```

### MediaUsage

```csharp
public class MediaUsage
{
    public int Id { get; set; }
    public int MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; }
    public string EntityType { get; set; }               // "Day", "ArtifactLibrary", "Unit", "LearningPath"
    public int EntityId { get; set; }
    public MediaUsageType UsageType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum MediaUsageType
{
    Featured,            // cover image for the artifact
    Inline,              // referenced in body content
    FlashcardFront,
    FlashcardBack,
    BingoCell,
    Background,
    Mascot               // decorative mascot illustration
}
```

### What we keep from EHF as-is

- The two-entity (Media + Usage) split
- The `FileHash` SHA256 dedup approach
- The `ThumbnailsJson` flexible-size dictionary
- Server-side download of external images (browser never contacts the external source directly)
- Force-delete with usage check
- Native Bootstrap-or-equivalent UI grid

### What we evolve

- `MediaSource` enum has four values from day one (not Unsplash-only)
- `MediaCategory` enum is curriculum-specific
- AI generation metadata (`AiPrompt`, `AiModel`) stored for re-generation and traceability
- License fields are more structured (separate type vs. text)

---

## Image source adapter pattern

```csharp
public interface IImageSourceAdapter
{
    string SourceName { get; }                   // "Pixabay", "Unsplash"
    MediaSource SourceEnum { get; }              // MediaSource.Pixabay, etc.
    bool IsAvailable { get; }                    // false if API key not configured
    Task<ImageSearchResults> SearchAsync(string query, int page, int perPage, CancellationToken ct = default);
    Task<ImageDetails> GetByIdAsync(string sourceId, CancellationToken ct = default);
    Task<byte[]> DownloadAsync(string url, CancellationToken ct = default);
    Task TriggerDownloadAsync(string sourceId, CancellationToken ct = default);  // Unsplash ToS requires; no-op for others
}
```

Implementations:
- `PixabayImageSourceAdapter` — primary. Pixabay has BOTH photos and vector illustrations (huge advantage for K4-8 worksheets that want cartoon clip art, not just photos). Free tier: 5000 requests / hour. Settings: `Pixabay:ApiKey`.
- `UnsplashImageSourceAdapter` — secondary. Best for cultural photos (Peru landscapes, food photography, real-world cultural scenes). Free tier: 50 requests / hour for demo apps. Settings: `Unsplash:AccessKey`, `Unsplash:SecretKey`.
- `AIImageGenerationAdapter` — Phase 4. Wraps your local Ollama / Stable Diffusion endpoint. Not a "search" model — exposes `GenerateAsync(prompt, style, size)` instead. Different interface (`IImageGenerationService`).
- `OpenClipartImageSourceAdapter` — Phase 4+. Public-domain vector clip art if Pixabay's coverage isn't enough.

The adapters are registered in DI; `IMediaService` queries all available ones for cross-source search. Users see results grouped by source with attribution clearly displayed.

---

## Document rendering

### Service contract

```csharp
public interface IDocumentRenderingService
{
    Task<string> RenderToHtmlAsync(IRenderable artifact, RenderContext context, CancellationToken ct = default);
    Task<byte[]> RenderToPdfAsync(IRenderable artifact, RenderContext context, CancellationToken ct = default);
    Task<byte[]> RenderBinderAsync(BinderComposition binder, RenderContext context, CancellationToken ct = default);
}

public interface IRenderable
{
    string Title { get; }
    string BodyMarkdown { get; }
    string ArtifactType { get; }                 // "Day", "Worksheet", "Game", "Flashcards", etc.
    GradeBand GradeBand { get; }
}

public class RenderContext
{
    public string ThemeName { get; set; } = "lcs-k2";
    public string? WatermarkText { get; set; }   // computed: "Property of LCS — Licensed to {Teacher} for {Period}"
    public PageSize PageSize { get; set; } = PageSize.Letter;
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
    public bool IncludeAttribution { get; set; } = true;   // emit Pixabay/Unsplash credits at end
}
```

### PDF generation: HTML + browser print-to-PDF (primary), QuestPDF (Phase 4 fallback)

**Decision rationale.** EHF uses QuestPDF for application PDFs (685 lines of fluent C# composition in `PdfService.cs`). PuppeteerSharp was the original plan here but it requires Chromium (~150 MB) and process-spawn permissions that may not work on SmarterASP. After comparing options, **the primary LCS path is HTML + CSS + browser print-to-PDF** — same pattern as the LearnedGeek proposal generator, just running inside the LCS web app:

- Server renders Markdown → HTML via Markdig + custom container extensions + kid-friendly CSS theme
- Teacher clicks "Generate Binder" → browser receives the styled HTML page
- Teacher Ctrl+P → "Save as PDF" → done
- Same CSS handles both screen preview and print output (via `@media print` and `@page` rules)

**Why this works for LCS:**
- Runs anywhere ASP.NET runs, including SmarterASP shared hosting (no native dependencies)
- Karen's authoring preview is byte-for-byte identical to the printed binder
- Adding new worksheet/game types is CSS + Markdig blocks, not C# layout components
- Zero server load per render (browser does the work)
- TPT-quality output is achievable purely in CSS (rounded sans typography, bright palette, decorative borders, illustration layouts)

**QuestPDF stays available for Phase 4 use cases** that require automated server-side PDF generation:
- "Email this binder to a teacher as an attachment"
- Scheduled overnight batch generation of fresh binders for the new period
- Archive PDFs stored alongside `BinderGeneration` log entries

When those needs arise, Mark already has working QuestPDF reference code in EHF (`Services/PdfService.cs`) — no learning curve. The LCS abstraction (`IDocumentRenderingService.RenderToPdfAsync`) leaves the implementation pluggable; we wire QuestPDF in when we need automated PDF output.

### Service contract (revised)

```csharp
public interface IDocumentRenderingService
{
    // Primary path: render HTML (preview, on-screen, browser-print friendly)
    Task<string> RenderToHtmlAsync(IRenderable artifact, RenderContext context, CancellationToken ct = default);
    Task<string> RenderBinderHtmlAsync(BinderComposition binder, RenderContext context, CancellationToken ct = default);

    // Phase 4: server-side PDF generation (QuestPDF-backed when needed)
    Task<byte[]> RenderToPdfAsync(IRenderable artifact, RenderContext context, CancellationToken ct = default);
    Task<byte[]> RenderBinderPdfAsync(BinderComposition binder, RenderContext context, CancellationToken ct = default);
}
```

For Phase 3, only the `*Html*` methods are implemented. The `*Pdf*` methods stub out (throw `NotImplementedException` with a clear message) until Phase 4.

### Binder rendering

A `BinderComposition` references multiple Days and ArtifactLibrary items. The HTML renderer:
1. Resolves teacher's `TeacherClassAssignment` for watermarking
2. Emits cover page (LCS logo, period, teacher name, watermark)
3. Emits each Day + its referenced artifacts in order
4. Emits attribution page (Pixabay/Unsplash photographers credited, LCS-branded styling)
5. Emits closing page (mascot, "Property of LCS" notice)
6. Whole thing is a single HTML document with `@media print` page-break rules
7. Teacher prints from browser; `BinderGeneration` row is logged at render-time, not at print-time (since print is client-side)

---

## Custom container blocks (Markdig extensions)

Implemented in the LCS web app's own `Markdown/LcsContainerExtensions.cs` (separate from the proposal generator's `MarkdownPreProcessor`).

### Lesson-flow blocks (shared across Days, Worksheets, Games)
- `:::lesson-overview` — at-a-glance card
- `:::warmup`, `:::teach`, `:::practice`, `:::assess`, `:::extend` — flow sections with time attributes
- `:::vocab` — vocab table with icon column
- `:::cultural-note` — boxed cultural callout with flag/icon
- `:::teacher-note` — teacher-only note (visible in teacher binder, hidden in student handout)
- `:::materials-checklist` — checkbox list

### Worksheet-specific blocks
- `:::trace` — dotted-line traceable text (CSS letter-spacing + dashed text-decoration)
- `:::color-target` — outlined coloring image with optional color hint
- `:::color-by-number` — numbered regions + color key legend
- `:::fill-in` — fill-in-the-blank with optional picture cues
- `:::matching` — left-column-to-right-column matching activity
- `:::cut-and-paste` — labeled cut-lines and target areas

### Game-specific blocks
- `:::bingo-card rows=3 cols=3` — CSS grid of colored cells with icons
- `:::memory-match pairs=8` — 4×4 card grid for cut-out memory game
- `:::flashcards layout="2-up"` — printable 2-up, 4-up, or 6-up card grid
- `:::board-game` — placeholder for board layouts (deferred to Phase 4)

### Reference blocks
- `:::game-ref slug="bingo-colores"` — embed-by-reference to another artifact
- `:::worksheet-ref slug="colores-trace-circle"` — same
- `:::vocab-ref slug="los-colores"` — reuse vocab from another Day

---

## Theme system

A theme is a CSS bundle + Razor partial set stored under `wwwroot/themes/{theme-name}/`.

### Initial themes
- `lcs-k2` — kid-friendly K4-2 (rounded sans like Quicksand/Nunito, bright primaries, big text 14-18pt, decorative dashed borders, illustration space)
- `lcs-3to5` — Phase 4: less decorative, more content-dense, age-appropriate fonts
- `lcs-6to8` — Phase 4: middle-school appropriate, closer to traditional textbook styling
- `lcs-teacher-binder` — special variant for teacher-only printables (note: this is the print *style*, not separate content)

Per-render theme selection via `RenderContext.ThemeName`. Default for a Day comes from its `GradeBand` (K4/K5/1/2 → `lcs-k2`; 3/4/5 → `lcs-3to5`; 6/7/8 → `lcs-6to8`).

---

## Authoring flows

### Karen authoring a Day
1. Admin → Curriculum → Days → Create
2. Form: title, grade band, theme, learning goals, body Markdown
3. Markdown editor with toolbar buttons: "Insert vocab table", "Insert game ref", "Insert image"
4. "Insert image" opens MediaLibraryPickerModal — search uploaded, Pixabay, Unsplash, or Generate (Phase 4)
5. Live preview pane shows rendered HTML in the kid-friendly theme
6. Save → creates `Day`, snapshots `CurriculumVersion`

### Karen creating a worksheet (or any artifact)
1. Admin → Curriculum → Artifacts → Create → choose type (Worksheet / Game / Flashcards / etc.)
2. Form: title, slug, parent Day (optional), grade band, theme override (optional)
3. Markdown body with artifact-type-aware custom block toolbar (worksheet form shows :::trace, :::color-target buttons; game form shows :::bingo-card, :::memory-match buttons)
4. Live preview
5. Save → creates `ArtifactLibrary` item, snapshots version

### Teacher generating a binder
1. Teacher dashboard → My Classes → pick class/period → "Build binder"
2. Step 1: Days. Lists Days assigned to that LearningPath; teacher checks which to include
3. Step 2: Artifacts. For each selected Day, lists recommended artifacts; teacher checks
4. Step 3: Extras. "Add extra artifact" → search ArtifactLibrary by theme/grade/keyword
5. Step 4: Generate. Server renders → PDF download
6. `BinderGeneration` log entry written with snapshot of versions used

### Karen managing the media library
1. Admin → Media Library → Index
2. Tabs: Browse Library | Upload | Browse Pixabay | Browse Unsplash | Generate (Phase 4)
3. Library tab: grid of all imported assets, filter by category/source/tag, click to view detail
4. Detail modal: edit metadata, view attribution, see usages, delete (with usage check)

---

## Authorization

Mirror EHF's `CanManageMedia` policy:
- `[Authorize(Policy = "CanManageMedia")]` on `Admin/MediaController`
- Policy requires `Admin` OR a future `MediaManager` claim
- `Teachers` get read-only access to the media library (for picking images into their own binder selections, not for editing)

---

## Phase rollout

### Phase 3 (September 2026 launch — must-have):
- `MediaAsset` + `MediaUsage` entities + EF migration
- `IMediaService` CRUD
- `PixabayImageSourceAdapter` (the primary search source for K4-2 content)
- `IImageProcessingService` (SixLabors.ImageSharp)
- Server-side upload + Pixabay import flow
- Media Library Index + Upload + Browse Pixabay UIs
- `IDocumentRenderingService` (Razor + PuppeteerSharp)
- `lcs-k2` theme (kid-friendly CSS)
- Custom blocks needed for Los Colores pilot: lesson-flow set + `:::vocab` + `:::bingo-card` + `:::trace` + `:::color-target` + `:::flashcards`
- Karen-facing authoring UI for Days + Artifacts
- Teacher-facing binder generation
- `BinderGeneration` audit log
- Watermarking from `TeacherClassAssignment`
- Attribution page at end of each binder

### Phase 4 (Fall semester — nice-to-have):
- `UnsplashImageSourceAdapter` (cultural photos)
- `IImageGenerationService` (Ollama integration)
- AI-generated images in Media Library with prompt history
- Additional custom blocks: `:::color-by-number`, `:::memory-match`, `:::matching`, `:::fill-in`, `:::cut-and-paste`
- `lcs-3to5` and `lcs-6to8` themes
- `OpenClipartImageSourceAdapter`

### Phase 5+ (future):
- Word search, maze, dot-to-dot blocks
- Print-shop offset printing variant (CMYK, bleed marks)
- Azure Blob / S3 storage for media (vs. wwwroot/uploads/)
- CDN integration

---

## Decisions locked (2026-05-23)

1. **Pixabay API key.** Mark has registered at https://pixabay.com/api/docs/ (free tier) and holds the key. Stored in **`appsettings.{Environment}.json`** following the existing SpanishScheduler convention (Stripe, ReCaptcha, Email all live in there). NOT in user secrets — that pattern requires per-machine setup and risks being missed at deploy time. See "Configuration and secrets convention" below.

2. **PDF approach.** HTML + CSS + browser print-to-PDF for Phase 3 (no native dependencies, runs on SmarterASP). QuestPDF deferred to Phase 4 for automated server-side PDF needs. EHF's `Services/PdfService.cs` is the reference implementation when we need it.

3. **Hosting.** SmarterASP remains the default. Hetzner becomes the Phase 4+ path when AI generation (Ollama) or automated server-side PDF batching is needed.

4. **Media storage in production.** Filesystem under `wwwroot/uploads/media/` per the EHF pattern. Not database BLOBs. Migrate to Azure Blob / S3 only if/when scale demands it (Phase 5+).

5. **AI generation timing.** Defer to Phase 4. Stub the `IImageGenerationService` interface in Phase 3 but no implementation. Local Ollama model setup is its own project later.

6. **Attribution page in binders.** Yes — last page of every binder lists photographer credit for any Pixabay/Unsplash images used. LCS-branded styling, not raw Pixabay/Unsplash branding. Renders at binder assembly time from the `MediaUsage` records of included artifacts.

7. **Markdown editor library.** EasyMDE for the authoring UI (open source, MIT, live preview, image-paste). Markdig stays as the server-side parser (already used in Mark's LearnedGeek blog, so it's a known quantity). If EasyMDE proves limiting later, swap to TUI Editor or a server-rendered preview approach.

## Configuration and secrets convention

Following the existing SpanishScheduler pattern (and matching how Stripe / ReCaptcha / Email config already work):

| File | Tracked in git? | Contents |
|---|---|---|
| `appsettings.json` | ✅ tracked | Base config + structural placeholders for every key the app reads. Real values are empty strings or default values. Lets any developer see what config exists at a glance. |
| `appsettings.Development.json` | ✅ tracked | Dev-environment overrides with placeholder secrets like `"sk_test_YOUR_TEST_KEY_HERE"`. Acts as a self-documenting template. Each developer replaces placeholders locally with their real dev keys. |
| `appsettings.Production.json` | ❌ gitignored | Real production secrets. Lives only on the production server (SmarterASP deployment target) and is managed manually OR rendered from a template via the deploy workflow. |
| `appsettings.Local.json` | ❌ gitignored | Optional per-developer override file. Already in `.gitignore` but not yet wired as a config source — to enable, add `AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)` in `Program.cs`. |

**Why this beats user secrets:**
- Per-machine `dotnet user-secrets set` requires explicit setup on every dev machine and is easy to miss when onboarding teammates
- User secrets live in `%APPDATA%/Microsoft/UserSecrets/...` on Windows (different path per OS) — not visible from the project directory, easy to lose track of
- The `appsettings.{Environment}.json` pattern produces uniform config loading across dev and prod — one place to look, one mental model

**For Pixabay specifically:**

Add to `appsettings.json`:
```json
"Pixabay": {
  "ApiKey": ""
}
```

Add to `appsettings.Development.json`:
```json
"Pixabay": {
  "ApiKey": "YOUR_PIXABAY_KEY_HERE"
}
```

Mark replaces the placeholder locally with the real key (or sets it in `appsettings.Local.json` if we wire that loader). Production reads from `appsettings.Production.json` on the SmarterASP server.

**Longer-term improvement (Phase 4 if helpful):**

The LearnedGeek deploy workflow (`E:\Documents\Work\dev\repos\LearnedGeek\.github\workflows\deploy.yml`) renders `appsettings.Production.json` from a `.template` file at deploy time using GitHub repo secrets:

```yaml
- name: Render appsettings.Production.json from template + secrets
  env:
    SMTP_PASSWORD: ${{ secrets.SMTP_PASSWORD }}
    ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
    # ... etc.
  run: |
    envsubst < LearnedGeek/appsettings.Production.json.template \
             > publish/appsettings.Production.json
    python3 -m json.tool publish/appsettings.Production.json > /dev/null
```

That keeps production secrets in GitHub repo secrets (single source of truth) and avoids manual SmarterASP filesystem edits. The current SpanishScheduler `deploy.yml` uses WebDeploy and assumes `appsettings.Production.json` is already on the target server. Worth adopting the LearnedGeek template-rendering approach for SpanishScheduler in Phase 4 — but not required for September launch.

---

## Estimated effort for Phase 3 (revised after PDF decision)

Rough breakdown assuming ~10 effective dev hours per week. The HTML+browser-print pivot cuts ~2 weekends off the original estimate by removing PuppeteerSharp setup and the bespoke PDF binder-assembly logic.

| Task | Weekends |
|---|---|
| Schema + migrations (MediaAsset, MediaUsage, Day, Unit, Path, etc.) | 1 |
| `IMediaService` + EF queries + tests | 0.5 |
| `IImageProcessingService` (SixLabors.ImageSharp wrap) | 0.5 |
| `PixabayImageSourceAdapter` + search/import flow | 1 |
| Media Library UIs (Index, Upload, Browse Pixabay) | 1.5 |
| `IDocumentRenderingService` HTML pipeline (Markdig + custom blocks + Razor templates) | 1 |
| Custom Markdig blocks (lesson-flow set + 5 worksheet/game blocks) | 1.5 |
| `lcs-k2` theme CSS (kid-friendly typography, palette, decorative borders, `@media print` rules) | 1 |
| Karen authoring UI for Days (EasyMDE-based editor + live preview) | 1 |
| Karen authoring UI for ArtifactLibrary items | 0.5 |
| Teacher binder generation flow (server-side HTML assembly + watermark + attribution page) | 0.5 |
| `BinderGeneration` audit log + version snapshotting | 0.5 |
| Integration tests | 0.5 |
| **Total** | **~9-10 weekends** |

**Realistic September scope:** ship the first 8-9 weekends of work (through teacher binder generation, watermarking, basic Karen authoring) by mid-August. Karen's actual content authoring (K4 "Los Colores" Unit and friends) happens in parallel through Phase 2 and overlaps with Phase 3 platform work. Cut scope items that aren't strictly necessary for September: the more advanced custom blocks (`:::matching`, `:::cut-and-paste`, etc.) move to Phase 4; only the blocks needed to render Los Colores ship in Phase 3.

QuestPDF + Unsplash + AI generation + additional themes all live in Phase 4, deferred deliberately.

---

## Next step

Architecture is locked (per the decisions section above). Phase 3 implementation order:

1. **Schema + migrations.** All curriculum and media entities in one EF migration: `Day`, `Unit`, `LearningPath`, `Period`, `TeacherClassAssignment`, `CurriculumVersion`, `BinderComposition`, `BinderGeneration`, `MediaAsset`, `MediaUsage`, `WisconsinStandard`. Seed WI Novice-band standards (already researched) at the end of the migration's data seed.
2. **`IMediaService` + `IImageProcessingService`.** EF CRUD + SixLabors.ImageSharp wrap. Basic upload endpoint and Media Library Index UI to verify it works.
3. **`PixabayImageSourceAdapter` + Browse Pixabay UI.** Requires Mark to register for a Pixabay API key first.
4. **`IDocumentRenderingService` HTML pipeline.** Markdig + custom container blocks + Razor partial per artifact type + `lcs-k2` CSS theme. Verify by rendering `los-colores.md` in the browser.
5. **Karen authoring UIs.** Days first, then ArtifactLibrary.
6. **Teacher binder generation flow.** Build the binder composition wizard + server-side HTML assembly + watermarking + attribution page.
7. **`BinderGeneration` audit log + integration tests.**

Each step is a clean weekend's work and produces something demonstrable before moving to the next.
