# Wisconsin World Languages Standards — K4 through 2nd Grade

**Research date:** 2026-05-21
**Researcher:** Mark (assisted)
**Intended use:** Seed data for `WisconsinStandard` table in Lake Country Spanish / SpanishScheduler curriculum subsystem (see `PLAN.md`).

---

## Source(s)

- **Primary URL:** https://dpi.wi.gov/sites/default/files/imce/standards/New%20pdfs/WorldLanguagesStandards2019.pdf
- **Landing page:** https://dpi.wi.gov/world-language/standards
- **Document title:** *Wisconsin Standards for World Languages* (Wisconsin Department of Public Instruction)
- **Publication date:** June 2019 (formally adopted June 3, 2019 by State Superintendent Carolyn Stanford Taylor)
- **Document length:** 49 pages, 42 numbered content pages
- **Authoritativeness:** This is the current adopted state standards document. The DPI standards landing page also references `WorldLanguagesStandards_7-31-21.pdf`, but the 2019 PDF is what is linked from search results and is published on DPI's `/imce/standards/` permanent path; the differences (if any) appear to be cosmetic. **Treat the June 2019 document as authoritative until DPI explicitly publishes a revision.**

---

## Framework Overview

Wisconsin organizes World Languages standards into **five standards** across **two goal areas**, integrating ACTFL's "Five Cs" (Communication, Cultures, Connections, Comparisons, Communities) into a Wisconsin-specific structure:

- **Goal Area: Communication (CM)**
  - Standard 1: Interpretive Communication (IT)
  - Standard 2: Interpersonal Communication (IP)
  - Standard 3: Presentational Communication (PS)
- **Goal Area: Cultural and Global Competence (CGC)**
  - Standard 4: Intercultural Communication (IC)
  - Standard 5: Global Competence and Community Engagement (GCE)

**Important divergence from the original PLAN.md assumption:** Wisconsin uses **three** communication standards (Interpretive / Interpersonal / Presentational), not five. Wisconsin does **not** separate Interpretive into "Listening" vs. "Reading" or Presentational into "Speaking" vs. "Writing" — instead each Interpretive/Presentational standard explicitly covers *"spoken, written, or signed"* language. The `Mode` enum in PLAN.md should be revised to match.

### Coding scheme

Wisconsin uses **proficiency-based codes, not grade-band codes.** The format is:

```
WL . <Standard> . <LearnerPractice> . <ProficiencyLevel>
```

- **Discipline:** `WL` (World Language)
- **Standard:** `IT` (Interpretive), `IP` (Interpersonal), `PS` (Presentational), `IC` (Intercultural), `GCE` (Global Competence and Engagement)
- **Learner Practice:** a number + letter, e.g. `1.a`, `2.b`, `4.c`
- **Proficiency Level (sub-level):**
  - Novice band: `n1` (Novice Low), `n2` (Novice Mid), `n3` (Novice High)
  - Intermediate band: `i4`, `i5`, `i6`
  - Advanced band: `a7`, `a8`, `a9`
  - Standards 4 and 5 use only `n+`, `i+`, `a+` (no sub-levels — designed for extension across the entire band over time)

**Example:** `WL.IT.1.a.n1` = World Language, Interpretive Communication, Learner Practice 1.a, Novice Low.

### Relationship to ACTFL / NCSSFL-ACTFL

- WI standards explicitly incorporate the **NCSSFL-ACTFL Can-Do Statements (2017)** as performance indicators for Standards 1–4 (with "minor stylistic edits focused on student performance").
- Proficiency bands come directly from the **ACTFL Proficiency Guidelines (2012)**.
- Intercultural *investigation* benchmarks are original to Wisconsin; intercultural *interaction* benchmarks are adopted from NCSSFL-ACTFL.

### Grade-band granularity — the critical caveat

**Wisconsin's standards are NOT grade-banded.** They are proficiency-banded across K-12. The document explicitly states (p. 16): *"The same standard and learner practice can be targeted across proficiency levels, languages, program models, and learner profiles with adaptations based on academic content, thematic focus, student proficiency level, and developmental age."*

Per the document (p. 14): *"students who study a language no longer than two years in high school may not move beyond Novice level language skills."* Conversely, an early-elementary K4-2 student in a standards-based program will operate firmly within the **Novice band (n1, n2, n3)**.

**Decision for this project:** For K4-2 seed data, we use the **Novice Low / Novice Mid / Novice High** performance indicators verbatim from the WI document. These are the indicators that K4-2 learners will actually be assessed against. The grade-band metadata (`GradeBand = "K4-2"`) is a Lake Country Spanish convention layered on top of WI's proficiency-only codes, not a WI-published distinction.

---

## Standards Applicable to K4-2nd Grade

All entries below are the **Novice band performance indicators** from the WI 2019 document (the proficiency level expected of K4-2 learners in a standards-based elementary program). Codes, descriptors, and indicator text are quoted verbatim from the source PDF except where noted.

Each entry follows: `<Code> — <Learner Practice descriptor (shortened)> — <Performance indicator>`.

### Standard 1: Interpretive Communication (IT)

> *"Students will use the language and cultural knowledge to interpret, analyze, and demonstrate understanding of authentic speech, texts, media, or signed language on topics relevant to their lives and broader communities."* (p. 21)

**Learner Practice 1.a — Authentic informational texts**

- `WL.IT.1.a.n1` — Interpret informational texts at Novice Low — *"Identify memorized or familiar words when they are supported by gestures or visuals in informational texts."*
- `WL.IT.1.a.n2` — Interpret informational texts at Novice Mid — *"Identify some basic facts from memorized words and phrases when they are supported by gestures or visuals in informational texts."*
- `WL.IT.1.a.n3` — Interpret informational texts at Novice High — *"Identify the topic and some isolated facts from simple sentences in informational texts."*

**Learner Practice 1.b — Authentic fictional texts**

- `WL.IT.1.b.n1` — Interpret fictional texts at Novice Low — *"Identify memorized or familiar words when they are supported by gestures or visuals in fictional texts."*
- `WL.IT.1.b.n2` — Interpret fictional texts at Novice Mid — *"Identify some basic facts from memorized words and phrases when they are supported by gestures or visuals in fictional texts."*
- `WL.IT.1.b.n3` — Interpret fictional texts at Novice High — *"Identify the topic and some isolated elements from simple sentences in short fictional texts."*

**Learner Practice 1.c — Authentic conversations and discussions**

- `WL.IT.1.c.n1` — Interpret conversations at Novice Low — *"Demonstrate understanding of memorized or familiar words when they are supported by gestures or visuals in conversations."*
- `WL.IT.1.c.n2` — Interpret conversations at Novice Mid — *"Identify some basic facts from memorized words and phrases when they are supported by gestures or visuals in conversations."*
- `WL.IT.1.c.n3` — Interpret conversations at Novice High — *"Demonstrate understanding of familiar questions and statements from simple sentences in conversations."*

### Standard 2: Interpersonal Communication (IP)

> *"Students use the target language and cultural knowledge to negotiate meaning through the exchange of information, ideas, reactions, feelings, and opinions in spoken, written, or signed interactions relevant to their lives and broader communities."* (p. 25)

**Learner Practice 2.a — Exchange information and ideas**

- `WL.IP.2.a.n1` — Exchange information at Novice Low — *"Provide information by answering a few simple questions on very familiar topics, using practiced or memorized words and phrases, with the help of gestures or visuals."*
- `WL.IP.2.a.n2` — Exchange information at Novice Mid — *"Request and provide information by asking and answering a few simple questions on very familiar and everyday topics, using a mixture of practiced or memorized words, phrases, and simple sentences."*
- `WL.IP.2.a.n3` — Exchange information at Novice High — *"Request and provide information by asking and answering practiced and some original questions on familiar and everyday topics, using simple sentences most of the time."*

**Learner Practice 2.b — Meet needs / address situations**

- `WL.IP.2.b.n1` — Meet needs at Novice Low — *"Express some basic needs, using practiced or memorized words and phrases, with the help of gestures or visuals."*
- `WL.IP.2.b.n2` — Meet needs at Novice Mid — *"Express basic needs related to familiar and everyday activities, using a mixture of practiced or memorized words, phrases, and questions."*
- `WL.IP.2.b.n3` — Meet needs at Novice High — *"Interact with others to meet basic needs related to routine everyday activities, using simple sentences and questions most of the time."*

**Learner Practice 2.c — Express preferences, opinions, feelings**

- `WL.IP.2.c.n1` — Express preferences at Novice Low — *"Express basic preferences or feelings, using practiced or memorized words and phrases, with the help of gestures or visuals."*
- `WL.IP.2.c.n2` — Express preferences at Novice Mid — *"Express one's own preferences or feelings and react to those of others, using a mixture of practiced or memorized words, phrases, and questions."*
- `WL.IP.2.c.n3` — Express preferences at Novice High — *"Express, ask about, and react to preferences, feelings, or opinions on familiar topics, using simple sentences most of the time and asking questions to keep the conversation on topic."*

### Standard 3: Presentational Communication (PS)

> *"Students use the target language and cultural knowledge to present information, concepts, and ideas on topics of relevance to their lives and broader communities to inform, explain, persuade, and narrate for diverse audiences within and beyond the learning environment."* (p. 29)

**Learner Practice 3.a — Describe lives, experiences, and events**

- `WL.PS.3.a.n1` — Describe self at Novice Low — *"Introduce self, using practiced or memorized words and phrases, with the help of gestures or visuals."*
- `WL.PS.3.a.n2` — Describe interests at Novice Mid — *"Present information about interests and activities using a mixture of practiced or memorized words, phrases, and simple sentences."*
- `WL.PS.3.a.n3` — Describe life at Novice High — *"Present personal information about life and activities, using simple sentences most of the time."*

**Learner Practice 3.b — Convey preference, opinion, persuasive argument**

- `WL.PS.3.b.n1` — Express likes at Novice Low — *"Express likes and dislikes using practiced or memorized words and phrases, with the help of gestures or visuals."*
- `WL.PS.3.b.n2` — Express likes at Novice Mid — *"Express likes and dislikes on very familiar and everyday topics of interest, using a mixture of practiced or memorized words, phrases, and simple sentences."*
- `WL.PS.3.b.n3` — Express preferences at Novice High — *"Express preferences on familiar and everyday topics of interest, using simple sentences most of the time."*

**Learner Practice 3.c — Inform, describe, or explain**

- `WL.PS.3.c.n1` — Name people/places at Novice Low — *"Name very familiar people, places, and objects using practiced or memorized words and phrases with the help of gestures or visuals."*
- `WL.PS.3.c.n2` — Present on familiar topics at Novice Mid — *"Present on very familiar and everyday topics using a mixture of practiced or memorized words, phrases, and simple sentences."*
- `WL.PS.3.c.n3` — Present on familiar topics at Novice High — *"Present on familiar and everyday topics using simple sentences most of the time."*

### Standard 4: Intercultural Communication (IC)

> *"Students use the target language and cultural knowledge to investigate, compare, explain, interact, and reflect on the relationships between the products, practices, and perspectives of diverse and dynamic cultures within their local and global communities."* (p. 32)

Note: Standards 4 and 5 use a single Novice indicator (`n+`) intended for extension throughout the band; teachers differentiate within the band locally.

**Learner Practice 4.a — Cultural products and perspectives**

- `WL.IC.4.a.n+` — Identify cultural products at Novice — *"Identify, in my own and other cultures, some typical products related to familiar everyday life."*

**Learner Practice 4.b — Cultural practices and perspectives**

- `WL.IC.4.b.n+` — Identify cultural practices at Novice — *"Identify some typical practices, in my own and other cultures, related to familiar everyday life."*

**Learner Practice 4.c — Interact with members of local/global community**

- `WL.IC.4.c.n+` — Interact with target-culture members at Novice — *"Communicate with others from the target culture in familiar, everyday situations using memorized language and showing basic cultural and linguistic awareness."*

**Learner Practice 4.d — Use culturally appropriate behaviors**

- `WL.IC.4.d.n+` — Culturally appropriate behaviors at Novice — *"Use appropriate rehearsed behaviors and recognize some obviously inappropriate behaviors in familiar, everyday situations."*

### Standard 5: Global Competence and Community Engagement (GCE)

> *"Students use the target language and cultural knowledge to investigate the world, recognize diverse perspectives, interact and exchange ideas with people from diverse backgrounds, and engage with others to improve conditions within their local and global communities."* (p. 34)

**Caveat for K4-2:** Standard 5 is, in practice, weighted toward older learners (research projects, position-taking on global issues). The Novice indicators are written abstractly enough to apply but may be aspirational for K4. Include in seed data but flag as "low-priority for K4-2 tagging" in the UI.

**Learner Practice 5.a — Examine local and global issues**

- `WL.GCE.5.a.n+` — Use evidence on a local/global issue at Novice — *"Use evidence from domestic and international sources to address a question with significance to their local and global community."*

**Learner Practice 5.b — Integrate diverse perspectives**

- `WL.GCE.5.b.n+` — Identify perspectives at Novice — *"Identify different personal and community perspectives on an issue of local and global significance."*

**Learner Practice 5.c — Exchange ideas across boundaries**

- `WL.GCE.5.c.n+` — Exchange perspectives at Novice — *"Exchange information and perspectives on an issue of local and global significance in linguistically and culturally appropriate ways."*

**Learner Practice 5.d — Engage to improve conditions**

- `WL.GCE.5.d.n+` — Plan and reflect on actions at Novice — *"Identify options, plan, take steps, and reflect on actions targeting an issue of local and global significance."*

**Learner Practice 5.e — Set language-learning goals**

- `WL.GCE.5.e.n+` — Goal-setting at Novice — *"Choose goals for language learning and use for personal or community life, and then monitor and reflect on progress toward those goals."*

---

## Standards Count Summary

| Standard | Learner Practices | Novice indicators (seed rows) |
|---|---|---|
| 1 — Interpretive (IT) | 3 (1.a, 1.b, 1.c) | 9 (n1/n2/n3 each) |
| 2 — Interpersonal (IP) | 3 (2.a, 2.b, 2.c) | 9 |
| 3 — Presentational (PS) | 3 (3.a, 3.b, 3.c) | 9 |
| 4 — Intercultural (IC) | 4 (4.a–4.d) | 4 (n+ only) |
| 5 — Global Competence (GCE) | 5 (5.a–5.e) | 5 (n+ only) |
| **Total** | **18 learner practices** | **36 performance indicators** |

This fits the requested "20-40 entries" target.

---

## Open Questions / Flags

1. **PLAN.md `Mode` enum needs revision.** PLAN.md currently lists `Interpretive-Listening / Interpretive-Reading / Interpersonal / Presentational-Speaking / Presentational-Writing / Cultural`. WI's actual modes are `Interpretive / Interpersonal / Presentational / Intercultural / GlobalCompetence`. Recommend changing the enum to match WI's structure, and adding a separate `SkillFocus` field (Listening / Reading / Speaking / Writing) on lessons or activities if Karen wants to tag that dimension independently — it's a useful pedagogical distinction even if WI doesn't enforce it.

2. **PLAN.md sample code format `WL.IL.K-2.1` does not exist in WI's framework.** WI codes look like `WL.IT.1.a.n1`. Recommend matching WI's format exactly so codes are searchable against the source document. If a grade-band suffix is needed in the database, add a separate `GradeBand` column rather than mutating the code.

3. **Wisconsin does not differentiate K4 from K5 from 1st from 2nd.** The entire K-2 (and arguably K-5) span maps to the Novice band. If Karen wants to differentiate developmentally appropriate expectations across K4 / K5 / 1st / 2nd, that differentiation is a **Lake Country Spanish curricular layer**, not a WI-standards layer. Suggest: map K4 → primarily n1; K5 → n1/n2; 1st → n2; 2nd → n2/n3. This is editorial, not authoritative.

4. **Standards 4 and 5 use `n+` (no sub-levels).** Decision needed: do we store one row per `n+`, or replicate to `n1/n2/n3` for UI consistency? Recommendation: one row per `n+` as published, with a `proficiency_sub_band` value of `Novice` (vs. `NoviceLow` / `NoviceMid` / `NoviceHigh` for Standards 1–3). Keeps the data faithful to the source.

5. **No "Connections" or "Comparisons" standalone strands.** WI integrated the Five Cs (Communication, Cultures, Connections, Comparisons, Communities) *into* the five standards rather than breaking them out. If Karen wants to tag a lesson as "this builds a Connection to math" or "this is a Comparison of cultures," that needs to be a separate tag/taxonomy — it's not a WI standard.

6. **Grade-band labeling.** WI uses "K-12" as a single span and proficiency for differentiation. "K4-2" is a Lake Country Spanish business label. The seed data should record `WI proficiency_level = NoviceLow/Mid/High` (or just `Novice` for Standards 4/5) and `LCS grade_band = "K4-2"` as separate fields so the lineage stays clean.

7. **Standard 5 fit for K4-2.** Several Standard 5 indicators reference "research," "domestic and international sources," "an issue of local and global significance" — written for older learners. Recommend marking Standard 5 indicators as `applicable_to_k4_2 = false` by default, or hiding them from the K4-2 tagging UI unless Karen explicitly opts in. Standards 1–4 are the natural fit for K4-2 lesson tagging.

8. **Potential newer version.** DPI's landing page references `WorldLanguagesStandards_7-31-21.pdf`. The 2019 PDF is what is currently indexed and search-discoverable, and the formal adoption date in the foreword is June 3, 2019. If a 2021 revision exists and differs materially, the codes and indicator wording may need a one-time refresh. **Action item:** before Karen tags lessons at scale, manually verify against the latest PDF DPI is serving.

---

## Suggested Seed Data Shape

For the `WisconsinStandard` table, each row should capture:

```
Code            : string   e.g. "WL.IT.1.a.n1"
Standard        : enum     Interpretive | Interpersonal | Presentational | Intercultural | GlobalCompetence
StandardNumber  : int      1..5
LearnerPractice : string   e.g. "1.a"
ProficiencyBand : enum     Novice | Intermediate | Advanced
ProficiencySub  : enum     Low | Mid | High | Unspecified  (Unspecified for Standards 4/5 "n+")
LearnerPracticeDescriptor : text  (shortened — what the practice IS)
PerformanceIndicator      : text  (verbatim from WI PDF — what the student does at this level)
SourceDocument            : string  "WI DPI Wisconsin Standards for World Languages, June 2019"
SourcePage                : int     (for traceability)
ApplicableToK4_2          : bool    (true for Standards 1–4, default false for Standard 5)
```

This shape lets the UI filter to "show me only K4-2-applicable Novice-band Interpretive standards" cleanly while preserving full fidelity to the source.
