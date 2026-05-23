# Day Format Specification

> **Terminology note (2026-05-21):** the data model entity is `Day` (matching Karen's mental model from her existing 8-Day units in `docs/karen-curriculum/`), but the authoring format described in this doc — Markdown + YAML + custom container blocks — applies equally to both Day teacher plans and standalone ArtifactLibrary items (worksheets, games, flashcards, newsletters). "Lesson" in this doc means "Day teacher plan" unless otherwise noted.

**Status:** Strawman draft, 2026-05-21
**Source pattern:** `E:\Documents\Work\dev\repos\learnedgeek-shared\proposals\ProposalGenerator` (Markdown + YAML front matter + Markdig custom containers)

## Why this format

The LearnedGeek proposal generator already produces polished, print-ready PDFs from Markdown via a browser-print pipeline. The same engine works for curriculum binders with:

- A different CSS theme (kid-friendly instead of professional)
- An extended set of custom container blocks (`:::vocab`, `:::game`, etc. instead of `:::phase`, `:::journey`)
- Per-binder dynamic data (teacher name, period, watermark) injected via YAML or preprocessor

No new PDF library, no Razor template churn, no rework of the existing engine. We extend the block vocabulary and swap the stylesheet.

---

## File anatomy

```markdown
---
title: "Los Colores"                # required — the lesson title
slug: "los-colores"                  # required — URL-safe ID, also used for file/asset paths
grade_band: "K4-2"                   # required — K4, K5, 1, 2, or compound like "K4-2"
level: "A1.0"                        # CEFR-aligned proficiency
duration_min: 20                     # estimated active teaching time
theme: "Colors"                      # high-level theme bucket
unit: "Mundo de Colores"             # optional — parent unit title
wi_standards:                        # WI standard codes this lesson maps to
  - WL.PS.3.c.n1                     # format: WL.<Standard>.<Practice>.<ProficiencyLevel>
  - WL.IT.1.a.n1                     # See wi-standards-research.md for full reference
materials:                           # what the teacher needs in the room
  - "Colored cards (rojo, azul, amarillo, verde)"
  - "Bingo cards (Bingo de Colores)"
  - "Stickers as token rewards"
accent: "#FF6B9D"                    # accent color for this lesson's binder pages
mascot_pose: "waving"                # which llama mascot illustration to use
artifacts:                           # printable companions to this lesson
  - { type: "worksheet", slug: "colores-trace-circle", title: "Trace & Color" }
  - { type: "game",      slug: "bingo-colores",        title: "Bingo de Colores" }
---
```

Lesson body follows the YAML, using standard Markdown plus the custom block vocabulary below.

---

## Custom block vocabulary

The proposal generator uses Markdig's `CustomContainers` extension (`:::name {attrs}` ... `:::`). We extend it with these block types for K4-2 lessons:

### `:::lesson-overview`
The "at a glance" section. Pulls duration, materials, key vocab count, learning targets. Often auto-renderable from YAML front matter; can also be hand-written.

### `:::warmup {time="N min"}`
Opening hook. The mascot greeting line, an attention-getter, a quick review of previous lesson.

### `:::vocab`
Vocabulary table. Standard columns: Spanish, English, Pronunciation, optional Image cue (filename or inline emoji). Renders with kid-friendly styling — big text, plenty of whitespace, illustration column on the right.

```markdown
:::vocab
| Español  | English | Pronunciation | Image       |
|----------|---------|---------------|-------------|
| rojo     | red     | ROH-hoh       | 🍎          |
| azul     | blue    | ah-SOOL       | 🐋          |
:::
```

### `:::teach {time="N min"}`
The main teaching activity. Step-by-step what the teacher does.

### `:::practice {time="N min"}`
Guided practice activity that doesn't require a separate printed artifact (e.g., "point to the color I say"). Brief.

### `:::game {type="..." prints="..." time="N min"}`
A game activity. The `type` attribute controls the game template (bingo, matching, board, scavenger-hunt, simon-says). The `prints` attribute names the companion artifact slug (links to a `LessonArtifact` of type Game-*). Body describes how to play.

```markdown
:::game {type="bingo" prints="bingo-colores" time="5 min"}
**Bingo de Colores**
1. Hand out bingo cards.
2. Call colors in Spanish, one at a time.
3. First to fill a row shouts "¡Bingo!" and wins a token sticker.
:::
```

### `:::worksheet {prints="..." time="N min"}`
Pointer to a printable worksheet artifact. Body briefly describes what the worksheet is and how to use it.

### `:::song`
Song or chant. Body has the Spanish lyrics + optional English translation in parallel.

### `:::cultural-note`
Cultural callout — a small box highlighting a Peruvian or pan-Hispanic cultural detail relevant to the topic.

### `:::assess`
Formative assessment — quick check for understanding before moving on. "Show me red. Show me blue."

### `:::extend`
Extension activity for kids who finish early or want a challenge.

### `:::teacher-note`
Pedagogical note for the teacher — does NOT print in the student/binder view if we decide to differentiate. Body is anything from "watch for the typical pronunciation slip on 'verde'" to "if you have a heritage speaker in the group, ask them to share what color X means at home."

### `:::materials-checklist`
Bulleted checklist of materials needed. Often auto-renderable from YAML; may also be hand-written.

---

## Artifact files (worksheets and games)

Each `LessonArtifact` is its own Markdown file in `artifacts/` named by slug:

```
docs/curriculum-system/
  samples/
    los-colores.md                 # the lesson
    artifacts/
      colores-trace-circle.md      # worksheet
      bingo-colores.md             # game (bingo card)
```

Artifact files use a simpler YAML and their own block set:

```yaml
---
artifact_type: "worksheet"      # or "game"
slug: "colores-trace-circle"
title: "Trace and Color the Colors"
lesson_slug: "los-colores"      # back-pointer to the parent lesson
grade_band: "K4-2"
print_css: "worksheet-k2"       # which CSS theme variant
copies_per_student: 1
---
```

Artifact-specific blocks:

- `:::trace` — render dotted-line traceable Spanish text
- `:::color-target` — placeholder for a coloring image
- `:::bingo-card {rows=3 cols=3}` — render a bingo grid
- `:::matching-pairs` — render a matching exercise (Spanish word ↔ image)
- `:::cut-and-paste` — a cut-and-paste activity layout

---

## Binder assembly

A binder is composed of:

1. **Cover page** — Lake Country Spanish branding + teacher name + period + grade band + unit/path title + copyright watermark
2. **Table of contents** — generated from the path/unit/lesson hierarchy
3. **Lesson pages** — one section per lesson, with the lesson body rendered first, then its artifacts inserted after the lesson
4. **Closing page** — Lake Country Spanish mascot, IP notice, "Property of LCS — Licensed to [Teacher] for [Period]"

The binder generator takes a `TeacherClassAssignment` ID, gathers the authorized `Lesson`s and `LessonArtifact`s for that assignment's `LearningPath`, snapshots the current `CurriculumVersion` of each, renders the combined Markdown through the proposal-generator pipeline, and produces an HTML file the teacher prints to PDF in their browser. A `BinderGeneration` row is written to the audit log.

---

## What stays the same

- The Markdig + custom container pattern from the proposal generator
- YAML front matter + placeholder substitution in the HTML shell template
- Browser-print-to-PDF (no server-side PDF library required for v1)
- Custom container block names follow `kebab-case` per the existing convention

## What's new

- The block vocabulary (everything above the dashes)
- A kid-friendly CSS theme (rounded sans-serif, bright accent palette, illustration room, US Letter portrait — distinct from the proposal-business theme)
- Per-lesson YAML extended with curriculum metadata (grade_band, theme, wi_standards, artifacts)
- Per-binder runtime data (teacher name, period, watermark) injected at render time

---

## Open questions before this is final

1. **Should `:::teacher-note` print in the teacher binder but not in a student handout, or always print?** Probably print in teacher binder; revisit when student-facing self-paced view exists.
2. **Image strategy.** Inline emoji is cheap but inconsistent. Linked SVG/PNG illustrations look better but require asset management. For K4-2 worksheets we'll likely want real illustrations — what's our source? (Public-domain image libs, AI generation, Karen's existing TPT-style assets?)
3. **Should artifact files live alongside lesson files in the repo, or as DB-stored Markdown that the admin UI edits?** Probably DB-stored once we have the schema; the `samples/` folder is the bootstrap.
4. **Custom block names** — these are a strawman, not final. Karen should weigh in once she sees the rendered output of the sample lesson.
