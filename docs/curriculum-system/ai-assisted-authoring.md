# LCS AI-Assisted Authoring — Design

**Status:** Design draft, 2026-05-24
**Author:** Mark, with Claude
**Supersedes:** the "Authoring flows" section of [`rendering-and-media-design.md`](rendering-and-media-design.md) (Karen authoring a Day, Karen creating a worksheet)
**Companion to:** rendering pipeline (still valid), media library (still valid), data model (still valid)

This document describes a fundamental change to how curriculum is authored on the LCS platform: from a CMS-style block-editor form, to an AI-mediated experience where Karen describes a lesson and the system drafts it.

---

## Why the existing approach is wrong

The block editor shipped on 2026-05-24 works — but it asks the author to think in the platform's schema. Karen sees fields like *Day # in unit*, *skill focus*, *grade band*, and the literal idea of "blocks" she has to insert and edit. None of these are concepts she uses in her teaching practice. The interface is, structurally, no better than a Word document with a custom toolbar.

The right framing isn't "build a better CMS form." It's: **Karen is the editorial brain; the platform is her teaching assistant.** She brings ideas and raw materials; the platform produces the structured, polished, print-ready lesson. The metadata is inferred. The structure is generated. The components are chosen for her. She reviews and refines, but never composes from scratch.

---

## The new authoring model

### One sentence

Karen describes a lesson in plain English, drops in any source materials she has, and the system drafts a complete Day with vocab tables, lesson-flow sections, bingo cards, worksheets, and a cultural note already wired up — ready for her to refine in a rendered preview.

### Five steps the author actually sees

1. **"Describe your lesson"** — a single textarea with a sample prompt as placeholder text. Optional supplemental inputs below: a media drawer to pick or upload images, a "reference docs" field for pasting old materials she wants to draw from, and a "what should students leave knowing" line for the learning goal.
2. **"Draft this"** — one button. The system sends her input + LCS component vocabulary to Claude, receives a structured response, persists it as the Day's block list.
3. **Rendered preview** — the lcs-k2 themed lesson page renders immediately. This is what Karen looks at.
4. **In-place refinement** — Karen clicks any section of the rendered preview to refine it. The refinement is *also natural language*: "Make the warmup shorter and more energetic" or "Replace the apple with a tomato for rojo" or "Add a Día de Muertos cultural connection." The system regenerates only that section.
5. **Approve and publish** — when she's happy, the Day flips to `IsActive` and becomes available for binder selection.

She never types Markdown, never sees `:::warmup`, never picks "Skill focus: Mixed" from a dropdown. The platform speaks her language and writes its own.

---

## How the AI generates valid blocks

Claude doesn't write Markdown. It writes structured block JSON that conforms to the existing block schema (`ParagraphBlock`, `VocabBlock`, `BingoCardBlock`, `TraceCardBlock`, `ImageBlock`, etc. — most of these don't exist yet beyond the three we shipped).

### System prompt structure

The Claude system prompt has three sections:

1. **Role and goal.** "You are a curriculum drafter for Lake Country Spanish. Given a teacher's brief, produce a structured K4-8 Spanish lesson using only the LCS component vocabulary."
2. **Component vocabulary.** A JSON schema of every available block type, what each is for, and what fields it accepts. Same schema the renderer and BlockCompiler already know about.
3. **Output contract.** "Respond with JSON matching this schema. No prose. No Markdown."

### Tool-use / structured output

We use Claude's structured output feature (response_format with a JSON schema, or tool-use with a `draft_lesson` tool whose input schema is the block list). This eliminates parsing failures: the response is always valid JSON that maps directly to our block types.

### What the AI infers vs. is told

The teacher's brief is freeform prose. From it, Claude infers:
- Grade band (from phrases like "for my K4 kids" or "second graders")
- Estimated duration (from "25-minute lesson" or by content density if unspecified)
- WI standards (by matching activities to standard descriptors — we include the WI standards as reference data in the prompt)
- Theme and unit fit (matched against existing Units; if no match, prompts for which Unit to attach to)
- Which components to use (vocab table when there's word lists; bingo when she mentions "game"; trace-card when she says "worksheet"; cultural-note when she mentions a country or tradition)

Karen can override any of these in a metadata panel that's collapsed by default. She only opens it when something looks wrong.

---

## In-place refinement (the WYSIWYG part)

The hardest UX question is: how does Karen edit the draft after it's generated?

### What we considered

- **ContentEditable on rendered HTML.** Edit the visible page directly. Hardest to round-trip back to source-of-truth blocks. Tempting but probably a footgun.
- **Per-block side-panel editor.** Click a block in the rendered preview, a side panel slides in with form fields. Closer to the block editor we already have; still asks Karen to think in fields.
- **Per-block natural-language refinement.** Click a block, a textarea pops up asking "What would you like to change here?" — she types in plain English ("shorten this and make it more playful"), and Claude regenerates that one block. We replace the block in the JSON and re-render.

### Recommended: per-block natural-language refinement

Keeps the promise that Karen never sees source. Same Claude-mediated experience as the initial draft, scoped to one block. Latency is bounded (one block, not a full lesson). Failure mode is clean: if Claude returns something invalid, we show the original with an error toast.

For tiny edits (typo, swap a single word), we may add a "quick edit" affordance that bypasses Claude — single-field input replacing the affected text directly. But the primary path is conversational.

---

## Storage and pipeline

### What stays exactly the same

- `Day.BodyBlocksJson` (just shipped) is still authoritative. AI output writes here.
- `IBlockCompiler` compiles blocks to Markdown unchanged.
- `IDocumentRenderingService` + Markdig + lcs-k2 theme renders Markdown to HTML unchanged.
- Binder assembly pipeline reads compiled Markdown and stitches lesson + artifacts unchanged.
- `MediaAsset` library and Pixabay adapter unchanged.
- `WisconsinStandard` reference data unchanged.

### What changes

- Block editor UI (the form I just shipped) moves to `/Curriculum/Days/{id}/Edit?advanced=1` and is hidden behind a "Power user mode" toggle. Not deleted, just demoted. Karen and Cece won't use it; future Mark might want it for debugging or for content imported from external sources.
- A new `ICurriculumDrafter` service wraps `IClaudeApiService` with the LCS-specific system prompt + schema enforcement.
- New endpoints on `CurriculumController`:
  - `POST /Curriculum/Days/Draft` — accepts a brief + assets, calls drafter, persists a new Day, returns redirect to its review page.
  - `GET /Curriculum/Days/{id}/Review` — renders the draft for review with click-to-refine affordances.
  - `POST /Curriculum/Days/{id}/Blocks/{blockId}/Refine` — accepts a natural-language change request for one block, calls drafter, updates one block, returns the re-rendered fragment.
- New view models for the brief form and the review surface.
- Metadata fields move from "always shown" to a collapsed `<details>` panel labeled "Override system inferences" — only useful when Karen disagrees with what Claude chose.

### What gets deprecated

- The current `/Curriculum/Days/Create` form (with its metadata-first design) gets replaced by `/Curriculum/Days/Draft`.
- The current `/Curriculum/Days/Edit/{id}` form becomes the *review and refine* surface. Block editor moves behind the `?advanced=1` toggle.
- The "Insert block" toolbar UI stays, but only renders in power-user mode.

---

## Architectural realities to plan around

### Claude dependency

This makes Claude a keystone. Three concerns:

1. **Cost.** A full lesson draft probably runs 3-8K tokens out, 2-4K in. At Claude rates that's pennies per draft, but it adds up if Karen drafts and refines 50 lessons. Worth metering in admin so we can see usage.
2. **Latency.** Full lesson draft: probably 8-15 seconds. We need a loading state that doesn't feel broken — progressive streaming the block list as Claude produces it is the ideal pattern (the user sees the lesson assembling).
3. **Reliability.** When Claude is down, drafting fails. We need a clean error path: "Drafting service is temporarily unavailable. You can use Power User Mode to compose blocks manually, or try again in a minute."

### Schema versioning

The block JSON schema *will* evolve. Adding `BingoCardBlock`, `TraceCardBlock`, `ImageBlock`, etc. means changing what Claude is allowed to produce. The drafter service needs a versioned prompt and a JSON schema version field on each Day so we can migrate old block JSON forward when the schema changes.

### Determinism and review

Karen will sometimes regenerate a lesson and get something different. That's a feature for exploration; it's a concern when she wants to make a small tweak and the whole structure changes. The refinement endpoint deliberately operates on one block at a time to prevent this — full regeneration only happens on her explicit request.

---

## What "Power User Mode" preserves

The block editor stays accessible (just hidden behind `?advanced=1`) because:

- Future Mark / dev maintenance: when something the AI produces is structurally weird, manual editing is the fastest fix.
- Importing existing content: pasting raw Markdown from her old Futura materials and converting it to blocks via the Raw Markdown block.
- Edge cases the AI handles poorly: very long lessons, unusual structures, content the AI has no training data for.
- Not blocking ourselves on AI: if Claude's down for a day, an admin can still create a Day.

It's not the primary path. It's the escape hatch. Calling it "Power User Mode" is honest framing — it's not for non-technical users.

---

## Phased rollout

### Phase 3a (next code session)
- `ICurriculumDrafter` service: wraps Claude, includes LCS system prompt, enforces block JSON schema
- `POST /Curriculum/Days/Draft` endpoint
- Brief form: single-page UI where Karen types her lesson description + selects images from media library
- "Drafting…" progress state
- Initial draft saved as a Day; redirect to review
- Review page reuses the rendered-preview pipeline we already have (no editing yet — just see the draft)

### Phase 3b
- In-place per-block refinement: click a block, type a refinement request, AI updates that block
- Quick-edit affordance for typo-level changes (bypass AI)
- Move existing block editor behind `?advanced=1`

### Phase 3c
- AI assist for vocab table (fill missing pronunciations, auto-suggest English from Spanish or vice versa)
- Image suggestion: AI examines lesson content and proposes images to search Pixabay for
- WI-standards alignment review: AI explains why each suggested standard fits

### Phase 4 (deferred)
- Multi-lesson drafting from a unit-level brief
- Voice input for the lesson brief
- Karen's own past lessons feed into the prompt as style examples ("write in Karen's voice")

---

## Decisions (locked 2026-05-24)

1. **Brief format:** single textarea. Don't overcomplicate.
2. **Iteration model:** one-shot per request. Chat-style is a Phase 4+ idea, not now.
3. **Grounding Karen's existing materials:** treat `docs/karen-curriculum/` as a **RAG corpus**, not few-shot examples. At draft time, retrieve the most relevant chunks for Karen's brief and include them in the Claude system prompt as context. Provides foundational data without trying to memorize all her materials in one prompt. See "RAG implementation" section below for the open sub-questions.
4. **Metadata override panel:** collapsed by default. The whole point of inference is that Karen doesn't think about these.
5. **AI unavailable fallback:** error toast only. Karen sees "Drafting is temporarily unavailable — please try again in a minute." No power-user mode link from her interface — that path stays a developer-only `?advanced=1` URL nobody surfaces.

## RAG implementation — open sub-questions

The RAG decision raises practical questions we don't have to answer before Phase 3a (which can ship without RAG), but do need to answer before 3b:

- **Vector store.** Postgres `pgvector` extension is the natural fit (we already run Postgres, no new service). Alternatives: SQLite with `sqlite-vec`, an in-memory FAISS-style index loaded at startup. Postgres+pgvector is the recommendation unless there's a deployment concern on Hetzner.
- **Embedding model.** Anthropic doesn't ship one. Three options:
  - **OpenAI `text-embedding-3-small`** — ~$0.02 / million tokens, well-supported, but introduces an OpenAI account/key in addition to Anthropic.
  - **Voyage AI** — Anthropic's recommended partner; their `voyage-3` model is competitive and keeps us on one credential surface (their API key).
  - **Local via Ollama** (e.g. `nomic-embed-text`) — free, no external dependency, but only viable once we're on Hetzner. SmarterASP can't host Ollama.
  - **Recommendation:** Voyage AI for Phase 3b; revisit local embeddings in Phase 4 when we move to Hetzner.
- **IP / licensing concern.** `docs/karen-curriculum/` is largely Karen's accumulated Futura materials. Using them as context for the AI to draft *LCS-original* lessons is fair use (we're not republishing them). But we should:
  - Never surface a retrieved Futura chunk verbatim in the rendered lesson.
  - Track which materials informed which draft for audit (a `LessonDraftSource` log).
  - Have a "redacted corpus" mode that strips identifying source markers from chunks before they reach the AI.

## Phase 3a scope (the first concrete code slice)

To avoid stacking too many new things in one slice, **Phase 3a ships without RAG**. We get the AI-assisted authoring shell working end-to-end with just the LCS component vocabulary + WI standards as system context. Once we see what Claude produces with that minimal context, we make a more informed call on the RAG investment.

3a deliverables:
- `ICurriculumDrafter` service + Claude system prompt + JSON schema enforcement
- "Describe your lesson" brief form (single textarea)
- "Drafting…" loading state with reasonable UX during the 8-15s wait
- Initial draft saved as a Day; redirect to a read-only review page
- Block editor moves behind `?advanced=1` toggle and stops being the default
- Error toast for unavailable AI

3b adds RAG (Voyage embeddings + pgvector + retrieval at draft time) and per-block refinement (the WYSIWYG-ish editing layer).

---

## What needs to be true before we start coding

- Decision on the open questions above (or "go with your recommendations" is fine too)
- Confirmation that we're OK pausing the block-editor polish and pivoting
- Confirmation of `IClaudeApiService` capacity and any rate / cost concerns
- Initial brief example we use for testing — probably "Los Colores, K4-2, 25 min" since we have the hand-authored version to compare against
