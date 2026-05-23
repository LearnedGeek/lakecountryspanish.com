# Lake Country Spanish — Curriculum System Plan

**Status:** Scoping draft, 2026-05-21
**Owners:** Mark (engineering), Karen (curriculum)
**Context:** Karen + several coworkers are discussing breaking off from their current contract teaching company to use lakecountryspanish.com as their own business. Curriculum availability is Karen's primary concern. This document scopes the curriculum subsystem.

---

## Vision

A WI-standards-aligned, **K4 through 8th grade** Spanish curriculum, owned by the Lake Country Spanish LLC as IP, with these defining principles:

### The strategic thesis — fluency readiness, not content looping

Most elementary/middle-school Spanish programs leave students at ACTFL **Novice Low** for 5+ years because they loop on the same content (colors, numbers, family) without advancing the proficiency target. LCS's actual product promise is the opposite:

> *"By 8th grade, our students are conversationally fluent at ACTFL Intermediate Mid/High. They can hold real conversations on familiar topics — not just label items in a picture book. Every grade has a measurable proficiency target, not a content list."*

The K4→8 progression LCS commits to (assumes ~20-30 min, 2-3× per week exposure; daily exposure accelerates, weekly slows):

| Grade | Target band | WI sub-level | What the student can do |
|---|---|---|---|
| K4 | Novice Low | n1 | Identifies memorized words with visual support |
| K5 | Novice Low → Mid | n1 → n2 | Recognizes familiar phrases; echoes simple sentences |
| 1st | Novice Mid | n2 | Answers simple questions on very familiar topics |
| 2nd | Novice Mid → High | n2 → n3 | Asks AND answers practiced questions |
| 3rd | Novice High | n3 | Speaks in simple sentences most of the time |
| 4th | Novice High → Intermediate Low | n3 → i4 | Initiates basic conversation on familiar topics |
| 5th | Intermediate Low | i4 | Holds conversations using strings of sentences |
| 6th | Intermediate Low → Mid | i4 → i5 | Combines sentences into connected discourse |
| 7th | Intermediate Mid | i5 | Narrates, describes, asks for clarification |
| 8th | Intermediate Mid → High | i5 → i6 | Sustains conversations; handles unexpected complications |

Honest dosage caveat: the table assumes ~20-30 min, 2-3×/week. Karen should be explicit about this assumption in LCS marketing — claiming Intermediate Mid by 8th grade on once-weekly exposure isn't defensible.

### Other defining principles

- **Fine-grained, forward-moving progression** — K4, K5, 1st through 8th are distinct grades, each with its own LearningPath, its own proficiency target (see fluency table above), and its own age-appropriate artifact mix. Same themes may recur across grades, but content depth, complexity, and proficiency expectation sharpen at each grade. Differentiation across grades is itself the product (a Lake Country differentiator vs. the "same content over wide grade bands" competitors).
- **Day-based teaching sessions** — the unit of organization is a `Day` (or `Session`), modeled on Karen's existing teaching practice: each Day combines a teacher plan + multiple supporting artifacts (newsletter, flashcards, cultural images, game, music, etc.).
- **Artifacts are first-class library items, not lesson-bound attachments** — teachers browse a rich library of worksheets/games/flashcards/etc. tagged by standard, theme, grade, skill, and difficulty. They assemble custom binders ad-hoc from this library based on what they're teaching that week. Variety is the value — the same standard might be covered by 4–5 different worksheets or 3 different games, and the teacher picks based on class energy, time available, and student needs.
- **Owned by the LLC, period-gated for teachers** — content stays with the LLC if a teacher leaves; per-teacher, per-period assignments control print rights with watermarked output.
- **Phase 2 ready** — data model supports self-paced student consumption between live classes and the token-redeemable-for-discount economy without rework.

## Scope

### In scope (Phase 1)

- Data model: `Day` (teaching session), `Unit`, `LearningPath`, `ArtifactLibrary` (with subtypes), `WisconsinStandard`, `TeacherClassAssignment`, `CurriculumVersion`, `BinderComposition`, `BinderGeneration`
- Day authoring format: Markdown + YAML front matter + custom container syntax (modeled on the LearnedGeek proposal generator)
- Admin curriculum builder (Karen authors Days, manages the ArtifactLibrary)
- Teacher browse + ad-hoc binder assembly UI (read-only on content, write on their own binder compositions)
- Binder PDF generation (reuses proposal-generator pipeline — Markdown → HTML → browser print to PDF)
- "Teacher" role (new — currently only Admin and Student exist)
- Watermarking, per-teacher per-period gating
- WI K4-2 World Languages standards as seed reference data (already researched — see `wi-standards-research.md`)

### Out of scope (Phase 2 — deferred)

- Student-facing self-paced lesson consumption between live classes (LCS's equivalent of Somos's Garbanzo platform at garbanzo.io — same Novice band content, interactive web delivery)
- `StudentLesson` progress tracking
- Auto-assignment of practice assignments after a lesson
- Mastery gates / prerequisites
- Tokens-redeemable-for-class-discount economy (data model partially exists; redemption flow is the new part)

### Explicitly NOT in scope

- 3rd grade and above (Karen's call — 3rd grade has different developmental expectations and should be a later phase if at all)
- User-generated curriculum from teachers (curriculum is owned by the LLC, authored by Karen)

---

## Data Model Sketch (Phase 1)

Architecture reshaped after Karen's input (2026-05-21): **Day** is the unit of teaching; **ArtifactLibrary** items are first-class, reusable, and ad-hoc composed into binders by teachers.

New entities under `Models/Entities/`:

```
Day  (a single teaching session — Karen's mental model from her existing units)
  Id, Title, Description
  GradeBand (single value: K4 | K5 | 1 | 2)
    -- per-grade differentiation: same theme may have separate Days per grade
  Theme (Colors, Shapes, Family, Animals-Jungle, Animals-Ocean, Food, ...)
  DayNumberInUnit (1..N — Karen's units are typically 8 days)
  TeacherPlanMarkdown (the teacher-facing lesson plan, rendered by the binder generator)
  EstimatedDurationMinutes
  CreatedById, ReviewedById, ReviewNotes  (mirror Assignment review pattern)
  Foreign keys to WisconsinStandard (many-to-many via DayStandard)
  Foreign keys to ArtifactLibrary items (many-to-many — RECOMMENDED artifacts, not bound)
  Foreign keys to Assignment (many-to-many — optional structured practice items)

ArtifactLibrary  (first-class catalog, not Day-bound)
  Id, Type (Newsletter | Flashcard | CulturalImageSet | Game | Worksheet | Song
            | StoryReader | VocabList | Craft | ReadingPassage | Award | Template | Other)
  Subtype (Game-Concentration / Game-Bingo / Game-Board / Game-ScavengerHunt /
           Game-RaceToIdentify / Worksheet-Trace / Worksheet-Color /
           Worksheet-ColorByNumber / Worksheet-FillIn / Worksheet-CutPaste /
           Worksheet-ReadingComp / Flashcard-Vocab / Flashcard-Phrase /
           Craft-Wheel / Craft-Mask / Craft-FamilyTree / Award-Diploma /
           Award-Badge / etc.)
  Title, Description
  GradeBands (many-to-many: an artifact can be tagged for K4 AND K5, etc.)
  Themes (many-to-many — most artifacts thematic; some generic)
  WisconsinStandards (many-to-many — what standards this artifact supports)
  SkillFocus (enum: Listening | Reading | Speaking | Writing | Mixed)
  Difficulty (Easy | Moderate | Challenging — within the grade band)
  BodyMarkdown OR AssetFilePath (printable or static asset)
  PrintCssVariant (worksheet-friendly, game-card, flashcard-grid, newsletter, ...)

Unit
  Id, UnitNumber (1, 2, 3, ...), Title, Description, GradeBand, DisplayOrder
  Theme grouping (a Unit holds Days on a related theme)
  TargetProficiencySubLevel (n1, n2, n3, i4, i5, i6 — the WI sub-level this unit moves
    students into; per the fluency table above)
  CoreVocabulary (list — vocab introduced in this unit; spirals into future units)
  CulturalConnection (named, first-class — what's the cultural tie-in?)
  SummativeAssessmentLinks (FK to Assignment(s) — what students do to demonstrate mastery)
  MinimumDurationDays (estimated minimum class periods)
  Foreign keys to Day (ordered — typically Day 1 through Day 8)
  Foreign keys to prior Units that this Unit assumes familiarity with
    (for the spiraled-curriculum prerequisite check)

LearningPath
  Id, Title, Description, GradeBand (single value: K4, K5, 1, 2, 3, 4, 5, 6, 7, 8 — each grade is distinct)
  TargetProficiencyLevel (the proficiency endpoint this path commits to —
    e.g., K4 = Novice Low; 5th = Intermediate Low; 8th = Intermediate Mid/High)
  Audience (Classroom, HomeSchool, After-School, Tutoring-1on1, Heritage-Speaker, Online, ...)
  Foreign keys to Unit (ordered)

BinderComposition  (a teacher's ad-hoc binder selection for a specific class/period)
  Id, TeacherClassAssignmentId, Title, CreatedAt, LastModifiedAt
  Foreign keys to Day (selected Days included)
  Foreign keys to ArtifactLibrary items (additional standalone artifacts the
    teacher pulled in beyond what each Day recommends)
  IsTemplate (bool — teacher can save a composition as a reusable template)

WisconsinStandard
  Id
  Code (e.g., WL.IT.1.a.n1)
  Standard (enum: Interpretive | Interpersonal | Presentational |
            Intercultural | GlobalCompetence)
  StandardNumber (1..5)
  LearnerPractice (e.g., "1.a")
  ProficiencyBand (enum: Novice | Intermediate | Advanced)
  ProficiencySub (enum: Low | Mid | High | Unspecified)
    -- Unspecified for Standards 4/5 which publish only "n+"
  LearnerPracticeDescriptor (short — what the practice IS)
  PerformanceIndicator (verbatim from WI PDF — what student does at this level)
  SourceDocument, SourcePage
  ApplicableToK4_2 (bool — default false for Standard 5; true for Standards 1-4)
  EffectiveDate

CurriculumVersion
  Id, LessonId, VersionNumber, EffectiveDate, ChangedById, ChangeNotes
  Snapshot of BodyMarkdown at this version
  (Lets us know what version of a lesson a teacher printed for a given period.)

TeacherClassAssignment
  Id, TeacherId (AspNetUser), PeriodId, GradeBand
  Foreign keys to LearningPath (or to individual Units / Lessons)
  PrintRights (none / per-lesson / per-unit / full-path)
  EffectiveStart, EffectiveEnd

Period
  Id, Name (e.g., "Fall 2026"), StartDate, EndDate

BinderGeneration
  Id, TeacherClassAssignmentId, GeneratedAt, LessonIds[], CurriculumVersionIds[]
  WatermarkText (computed: teacher name + period + copyright)
  (Audit log: what got printed, when, by whom, which versions.)
```

### Notes on the model

- **Versioning** lives at the Day and ArtifactLibrary levels (`CurriculumVersion`). Karen edits Days and Artifacts; units and paths are just ordered containers.
- **Per-grade differentiation is the product.** Each grade (K4 / K5 / 1 / 2) gets its own LearningPath with its own Units and Days. A "Colors" theme may have separate K4 Colors Day, K5 Colors Day, etc. — same theme, age-appropriate depth and artifact mix. This is the Lake Country differentiation: NOT "same content for K-2."
- **Ad-hoc binder assembly is the teacher's main workflow.** A teacher in Karen's network teaching Spring 2026 K4 picks Days from the K4 LearningPath, then augments by pulling additional artifacts from the library (e.g., "I want one more concentration game on family vocab for this week"). The `BinderComposition` is their saved selection; rendering it produces the watermarked PDF.
- **Spiraled curriculum (Somos pattern).** Core Vocabulary introduced in a Unit recycles into subsequent Units. The `Unit.PriorUnitDependencies` field lets us auto-suggest prerequisite checks: "Day 12 assumes familiarity with vocab from Units 1 and 3 — confirm your students have those before starting." Teachers can override.
- **Cultural Connection is first-class, not a sidebar.** Every Unit has a named cultural tie-in (Somos pattern). Maps naturally to WI Standard 4 (Intercultural Communication). "None" is a valid value when no tie-in fits — don't pad for completeness.
- **Print rights** are at the assignment level. A teacher could be assigned full-path print rights for K-2 Spanish during Fall 2026, but only browse rights during the off-season.
- **Browse vs. print** as separate permissions is what lets teachers "grab new curriculum ideas" while preventing them from extracting the entire library on the way out.
- **CurriculumTopic** (already in the codebase) and **Theme** (proposed here) overlap and may need reconciliation. Worth a 5-minute alignment pass when we start coding.
- **WI standards are proficiency-banded, NOT grade-banded.** Earlier draft of this doc assumed a 5-mode listening/reading/speaking/writing/cultural split — that's the ACTFL pedagogical framing, NOT what WI publishes. WI uses 3 communication standards (Interpretive / Interpersonal / Presentational) where each covers spoken, written, OR signed, plus 2 cultural standards (Intercultural / Global Competence). See `wi-standards-research.md` for the full structure.
- **Grade band is an LCS layer, not a WI layer.** WI maps the entire K-12 range to proficiency bands (Novice / Intermediate / Advanced). K4-2 learners all operate within the **Novice band**. The `Lesson.GradeBand` field (K4 / K5 / 1 / 2) is Lake Country Spanish's editorial layer for "which kids is this lesson appropriate for" — independent of which WI standards the lesson tags. Suggested editorial mapping: K4 → Novice Low; K5 → Novice Low / Mid; 1st → Novice Mid; 2nd → Novice Mid / High.
- **Optional `SkillFocus` field** on Lesson (Listening / Reading / Speaking / Writing) is pedagogically useful even though WI doesn't enforce that split. Worth adding so Karen can tag lessons by skill mode independently of WI standard tags.

---

## Build Sequencing

> **Timeline pressure (2026-05-21):** Karen and Cece are targeting **September 2026** to launch for the Fall school semester. That gives ~3 months (June-August) for the platform build + initial curriculum population. Sequencing reflects this.

> **Immediate priority shift:** The very first concrete code task is now the **Teacher role** so Cece (Karen's friend who is already browsing the site and has explicitly asked to be added as a Teacher) can be onboarded. Curriculum data model + builder are second priority, behind getting the first real teacher onto the platform.

1. **Pre-work (no code, partly in flight):**
   - WI K4-2 Novice-band standards research → DONE (see `wi-standards-research.md`)
   - **Follow-up WI standards research for Intermediate band (i4/i5/i6)** to cover grades 4-8 — TBD
   - One TPT worksheet sample from Karen → visual reference for CSS theme (we now have several in `karen-curriculum/TPT/`)
   - Karen's full topic list across K4-8 — TBD; she'll need help splitting authoring by grade across the breakaway teacher group

2. **Format pilot (small):**
   - Draft 1 lesson + 1 worksheet + 1 game in the proposed Markdown format (in `samples/`)
   - Render through the proposal generator (or a stripped-down copy) — see if the output reads as a kid-friendly binder page
   - Iterate on the block syntax until Karen says "yes, like that"

3. **Schema work:**
   - EF Core migration for the new entities above
   - Reconcile `CurriculumTopic` (existing) with `Theme` (proposed)
   - Seed WI standards from the research output

4. **Admin curriculum builder:**
   - Karen-facing UI to author Lessons, Units, LearningPaths
   - Reuses the existing Assignment review workflow pattern

5. **Teacher role** *(promoted to immediate priority — Cece is waiting):*
   - New AspNet Identity role
   - Teacher invitation / onboarding flow (admin invites by email; teacher accepts and sets up profile)
   - Teacher dashboard (`TeacherController` + `Views/Teacher/`)
   - For v1, even before curriculum builder ships: teacher can log in, see their profile, see their assigned class(es) and period(s). Browse + print authorized binders comes once the curriculum data exists.

6. **Binder generation:**
   - Reuse the LearnedGeek proposal-generator pipeline
   - Custom CSS theme: kid-friendly (rounded sans-serif, bright accent palette, illustration room, big readable type)
   - Watermarking from `TeacherClassAssignment` + `Period`
   - `BinderGeneration` audit log entry on each render

7. **Phase 2 (deferred):**
   - Student lesson view, `StudentLesson` tracking, auto-assignment, mastery gates, token-to-Stripe-discount flow

---

## Open questions

- **`CurriculumTopic` vs. `Theme`** — existing entity vs. proposed. Reconcile when we touch the schema.
- **Worksheet/game printables: separate PDFs or one combined binder?** Likely combined for teachers' convenience, but worth confirming with Karen.
- **Visual style baseline for K4-2 worksheets** — pending one TPT exemplar from Karen.
- **Period model granularity** — semester? quarter? rolling 12-week sessions? Karen and the teacher group should pick what matches their actual operating cadence.
- **Print rights at what level?** Per-lesson is finest; per-path is coarsest. Per-unit is a likely middle ground. Karen's call.

## Action items before coding

- [ ] **Verify WI standards document version.** DPI's landing page references both `WorldLanguagesStandards2019.pdf` (the one we cited) and a `_7-31-21.pdf` variant. The June 2019 PDF is what's indexed and search-discoverable; the 2021 variant may be cosmetic or substantive. **One manual check before Karen tags lessons at scale.** Flagged by the standards research agent.

- [ ] **Follow-up WI standards research for the Intermediate band (i4/i5/i6).** Original research covered K4-2 → Novice band only. With LCS scope now K4-8, grades 4-8 will tag against Intermediate band indicators. Single research pass to extract those from the same 2019 PDF — should be quick since the document covers all bands.

- [ ] **Reference: existing curriculum structure to learn from.** `docs/karen-curriculum/` contains Karen's actual 8-Day unit packets (Harland North, StPauls — multi-year iterations) from her 18 years at Futura Language Professionals. Each Day has a Newsletter (parent communication), Cultural Images, Flashcards, sometimes a Game (e.g., "concentration template" = memory matching). Themes observed: la familia, Carnival, alphabet, snowman, greetings. This is the structural template our `Day` + `ArtifactLibrary` model is shaped to support.

- [ ] **Reference: TeachersPayTeachers exemplars** in `docs/karen-curriculum/TPT/`. Notable items:
  - **Somos 1 Unit 1 + Resource Map** (The Comprehensible Classroom, comprehensibleclassroom.com/somos) — the closest commercial analog to LCS in BUSINESS MODEL, though it targets middle/high school (Somos = Novice accelerated for adolescents; LCS = Novice slowed down for K4-2). Per-teacher license ($200-542/level), watermarked "LICENSED ACCESS DOCUMENT — DISTRIBUTION PROHIBITED," Google Drive distribution, transferable-license rules. **The Resource Map structure (table with columns: Unit Number, Core Vocabulary, Cultural Connection, Summative Assessment, Duration) is what shaped our `Unit` entity above.** Also: Somos's interactive web platform at garbanzo.io is the Phase 2 analog for LCS's self-paced student platform.
  - **Lita Lita "Days of the Week wheel"** — brass-fastener craft wheel example (informs `Craft-Wheel` subtype)
  - **Sarah Svatos "Cinco de Mayo Mystery Picture"** — color-by-number visual puzzle (informs `Worksheet-ColorByNumber` subtype)
  - **The Engaged Spanish Classroom "Taco Tuesday"** — race-to-identify game mechanic transferable across age bands (informs `Game-RaceToIdentify` subtype)
  - **Maestra Ana Maria "Lecturas Primero/Segundo"** — 1st/2nd grade reading comprehension worksheets (informs `Worksheet-ReadingComp` subtype; format only applies to the upper end of our K4-2 band)
  - **FREE Teacher binder / Awards/Diplomas** — informs `Award-Diploma` / `Award-Badge` subtypes (candidate token-redemption rewards)

**IP note (acknowledged constraint, not a blocker):** Both `karen-curriculum/` (Karen's Futura-branded content) and `karen-curriculum/TPT/` (purchased third-party licensed materials) are **reference material for ideas, pedagogy, structure, and visual quality bar only — never for direct asset redistribution.** All concrete assets shipped in LCS binders will be LCS-original. Same rule applies to both sub-folders.

---

## Sibling docs

- `lesson-format.md` — the Markdown + YAML + custom block syntax spec
- `samples/los-colores.md` — sample lesson in the proposed format
- `wi-standards-research.md` — seeded by background agent
