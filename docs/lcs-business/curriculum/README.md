# LCS curriculum

Working folder for Karen + Cece's curriculum content and the
LCS-branded renders of it. Three subfolders:

| Folder | What's in it |
|---|---|
| `sources/karen/` | Raw lesson drafts Karen sent (`.docx`) — Los Colores, Los Números 1-20, La Familia, etc. |
| `sources/cece/` | Raw drafts from Cece (`.pages` from Mac + a `.pdf` export). |
| `sources/shared/` | Shared / unattributed drafts — scope-and-sequence, etc. |
| `proposals/` | LCS-branded renders of the lessons in the agreed teacher-binder format (Lora/Source-Sans-3, dual-labeled WI DPI + ACTFL standards, tiered vocab). One `.md` (source) + `.html` (printable) per lesson. |

## Conventions

- **Filenames** are lowercase-hyphenated (`la-familia.docx`, not `LCS la familia.docx`). Renames at intake.
- **Same lesson** has the same base name across folders, so `sources/karen/la-familia.docx` → `proposals/la-familia.md` + `.html`.
- **Sources are untracked** in git (they're working drafts that come and go). Proposals are tracked.

## Pending — autogeneration

Once the proposal format is settled, the natural next step is moving from hand-authored Markdown
to a system Karen can drive directly: form-based authoring backed by the existing `Day` / `Unit` /
`LearningPath` entities, vocab pulled from the `VocabTerm` catalog (the system already powering
worksheet image generation), Razor renderer emitting the same teacher-binder template.
