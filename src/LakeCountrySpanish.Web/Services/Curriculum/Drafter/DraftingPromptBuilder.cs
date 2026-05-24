using System.Text;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Services.Curriculum.Drafter;

/// <summary>
/// Builds the Anthropic system prompt for the curriculum drafter and the
/// JSON tool-input schema that forces structured output. Separated from
/// <see cref="CurriculumDrafter"/> so the prompt is independently testable
/// and reviewable as a piece of editorial content.
/// </summary>
public sealed class DraftingPromptBuilder
{
    private readonly ApplicationDbContext _context;

    public DraftingPromptBuilder(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the system prompt + the tool input_schema as a JSON string.
    /// Tool name is fixed: <c>submit_lesson_draft</c>.
    /// </summary>
    public async Task<DraftingPrompt> BuildAsync(DraftLessonRequest request, CancellationToken ct)
    {
        var units = await _context.Units
            .Where(u => u.IsActive)
            .OrderBy(u => u.UnitNumber)
            .Select(u => new { u.Id, u.Title, u.Theme, u.CoreVocabulary, u.CulturalConnection })
            .ToListAsync(ct);

        var standards = await _context.WisconsinStandards
            .OrderBy(s => s.Code)
            .Select(s => new { s.Code, s.PerformanceIndicator, s.LearnerPractice })
            .ToListAsync(ct);

        var system = BuildSystemPrompt(units, standards);
        var schema = BuildToolInputSchema(units);
        var userMessage = BuildUserMessage(request);

        return new DraftingPrompt(system, schema, userMessage);
    }

    private static string BuildSystemPrompt(
        IReadOnlyList<dynamic> units,
        IReadOnlyList<dynamic> standards)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a curriculum drafter for Lake Country Spanish (LCS), a K4–8 Spanish language program in Wisconsin. Your job is to take a teacher's plain-English description of a lesson she wants and produce a structured lesson plan using ONLY the LCS component vocabulary defined below.");
        sb.AppendLine();
        sb.AppendLine("## Pedagogical context");
        sb.AppendLine();
        sb.AppendLine("- LCS lessons target the Wisconsin DPI World Languages standards. Novice-band lessons (K4–2nd grade) build basic word recognition, repetition, and simple cultural awareness.");
        sb.AppendLine("- LCS teachers are native or near-native Spanish speakers. Lessons should use Spanish vocabulary in context, with English used sparingly for instructions.");
        sb.AppendLine("- The LCS mascot is Lola the llama. References to Lola can appear in warm-ups and game framing where natural; do not force her in.");
        sb.AppendLine("- Lessons are typically 20–30 minutes long. Each lesson has a warm-up, a teach segment, a practice activity, an assessment (informal — usually a thumbs-up check, not a quiz), and optional cultural and extension content.");
        sb.AppendLine();
        sb.AppendLine("## LCS lesson component vocabulary");
        sb.AppendLine();
        sb.AppendLine("A lesson is a sequence of typed blocks. Each block has a `type` field and type-specific fields. The complete list of block types you may use:");
        sb.AppendLine();
        sb.AppendLine("### paragraph");
        sb.AppendLine("A section of prose. Use this for every lesson-flow section. The `containerKind` field tells the renderer which themed container to wrap the prose in — colored borders, section labels, etc. Allowed `containerKind` values:");
        sb.AppendLine();
        sb.AppendLine("- `lesson-overview` — single block at the top summarizing theme, target audience, learning goal. No `time` attribute.");
        sb.AppendLine("- `warmup` — opening hook, 2–4 minutes. Include `time` like \"3 min\".");
        sb.AppendLine("- `teach` — direct instruction, 5–10 minutes. Include `time`.");
        sb.AppendLine("- `practice` — guided/active practice, 3–8 minutes. Include `time`.");
        sb.AppendLine("- `assess` — informal exit check, 1–3 minutes. Usually no `time`.");
        sb.AppendLine("- `extend` — optional enrichment for early finishers.");
        sb.AppendLine("- `cultural-note` — country-specific cultural tie-in. Should reference a specific country (often Peru, but any Spanish-speaking country fits).");
        sb.AppendLine("- `teacher-note` — internal note for the teacher only (differentiation, common errors, next-lesson preview). Not student-facing.");
        sb.AppendLine("- `materials-checklist` — bulleted list of physical materials the teacher needs. Use Markdown task-list syntax (`- [ ] item`).");
        sb.AppendLine("- `null` / omitted — plain prose with no container wrapping. Use rarely.");
        sb.AppendLine();
        sb.AppendLine("The prose `text` may use light inline Markdown: **bold**, *italic*, simple bullet lists. Do not include `:::container` syntax — the container wrapping is handled by `containerKind`.");
        sb.AppendLine();
        sb.AppendLine("### vocab");
        sb.AppendLine("A structured vocabulary table. Use this whenever you introduce new words. Each row has `spanish`, `english`, `pronunciation` (English-phonetic, e.g. \"ROH-hoh\" for rojo), and `cue` (a short visual mnemonic, often a single emoji like 🍎). Keep tables to 4–8 rows for K4–2.");
        sb.AppendLine();
        sb.AppendLine("### raw");
        sb.AppendLine("Escape hatch — arbitrary Markdown including LCS container blocks (`:::bingo-card`, `:::trace-card`, etc.). Use this when the lesson genuinely needs a game grid or worksheet structure. Bingo cards are a 3×3 Markdown table inside `:::bingo-card`. Trace cards are inside `::::worksheet-grid` wrapping multiple `:::trace-card {accent=\"red|blue|yellow|green|orange|purple\"}` blocks, each with a word on the first line and an emoji target on the second.");
        sb.AppendLine();
        sb.AppendLine("## Available Units (pick the one whose theme best matches the brief)");
        sb.AppendLine();
        if (units.Count == 0)
        {
            sb.AppendLine("- (no units defined yet — assign unitId 0)");
        }
        else
        {
            foreach (var u in units)
            {
                sb.AppendLine($"- **Id {u.Id}**: {u.Title} (theme: {u.Theme}) — core vocab: {u.CoreVocabulary}; cultural tie: {u.CulturalConnection}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Wisconsin DPI World Languages standards (Novice band)");
        sb.AppendLine();
        sb.AppendLine("Select 2–5 standard codes that the lesson genuinely targets. Don't pad — fewer well-matched codes is better than many loose matches.");
        sb.AppendLine();
        foreach (var s in standards.Take(36))
        {
            sb.AppendLine($"- `{s.Code}` ({s.LearnerPractice}): {s.PerformanceIndicator}");
        }
        sb.AppendLine();
        sb.AppendLine("## Structural rules");
        sb.AppendLine();
        sb.AppendLine("These rules keep the rendered lesson readable and consistent across teachers.");
        sb.AppendLine();
        sb.AppendLine("- **Every paragraph block must have a `containerKind`.** Do not emit unwrapped prose. If a paragraph supports a game or worksheet — for example explaining how Bingo de Colores is played — put that explanation inside the relevant lesson-flow section (usually `practice` or `teach`), or place it as the prose body that precedes the raw block within the same section. Never leave a free-floating paragraph between blocks.");
        sb.AppendLine("- **`materials-checklist` goes at the end of the lesson**, not at the top. The teacher reads the lesson first; the checklist is what she gathers on the way out the door.");
        sb.AppendLine("- **`lesson-overview` goes first**, exactly once.");
        sb.AppendLine("- **`teacher-note` and `cultural-note`** appear where they're relevant in the flow — `cultural-note` usually near the end before assess; `teacher-note` last (after assess) so it doesn't interrupt the student-facing content.");
        sb.AppendLine("- **Vocab tables** come after the warmup and before the main practice activity, so students see the words once before they're asked to use them.");
        sb.AppendLine();
        sb.AppendLine("## Output");
        sb.AppendLine();
        sb.AppendLine("Use the `submit_lesson_draft` tool to return your draft. Do not respond with prose. Do not include the `:::container` Markdown syntax in `paragraph` blocks — use the `containerKind` field instead.");
        return sb.ToString();
    }

    private static string BuildUserMessage(DraftLessonRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Teacher's brief:");
        sb.AppendLine();
        sb.AppendLine(request.Brief?.Trim());
        sb.AppendLine();
        if (request.UnitId.HasValue)
        {
            sb.AppendLine($"(Force unit id: {request.UnitId.Value})");
        }
        if (request.GradeBand.HasValue)
        {
            sb.AppendLine($"(Force grade band: {request.GradeBand.Value})");
        }
        sb.AppendLine();
        sb.AppendLine("Call `submit_lesson_draft` with your structured response.");
        return sb.ToString();
    }

    /// <summary>
    /// JSON schema for the <c>submit_lesson_draft</c> tool. Forced via
    /// <c>tool_choice: { type: "tool", name: "submit_lesson_draft" }</c>.
    /// Mirrors <see cref="Blocks.Block"/> and its discriminated subclasses.
    /// </summary>
    private static string BuildToolInputSchema(IReadOnlyList<dynamic> units)
    {
        var unitIds = units.Count == 0 ? "0" : string.Join(", ", units.Select(u => u.Id));
        return $$"""
        {
          "type": "object",
          "required": ["title", "theme", "unitId", "gradeBand", "estimatedDurationMinutes", "wiStandardCodes", "blocks"],
          "properties": {
            "title":   { "type": "string", "description": "Display title for the lesson (e.g. 'Mundo de Colores — Day 1')." },
            "theme":   { "type": "string", "description": "Short thematic label (e.g. 'Colors')." },
            "unitId":  { "type": "integer", "description": "Id of the parent Unit. Pick one of: {{unitIds}}." },
            "gradeBand": {
              "type": "string",
              "enum": ["K4","K5","Grade1","Grade2","Grade3","Grade4","Grade5","Grade6","Grade7","Grade8"]
            },
            "estimatedDurationMinutes": { "type": "integer", "minimum": 5, "maximum": 90 },
            "wiStandardCodes": {
              "type": "array",
              "items": { "type": "string" },
              "minItems": 1,
              "maxItems": 5,
              "description": "WI standard codes the lesson targets, e.g. ['WL.PS.3.c.n1', 'WL.IT.1.a.n1']."
            },
            "blocks": {
              "type": "array",
              "minItems": 3,
              "description": "Lesson body as an ordered list of typed blocks.",
              "items": {
                "oneOf": [
                  {
                    "type": "object",
                    "required": ["type", "text"],
                    "properties": {
                      "type": { "const": "paragraph" },
                      "text": { "type": "string" },
                      "containerKind": {
                        "type": ["string", "null"],
                        "enum": ["lesson-overview","warmup","teach","practice","assess","extend","cultural-note","teacher-note","materials-checklist", null]
                      },
                      "time": { "type": ["string", "null"], "description": "Time hint like '3 min' for lesson-flow sections." }
                    }
                  },
                  {
                    "type": "object",
                    "required": ["type", "rows"],
                    "properties": {
                      "type": { "const": "vocab" },
                      "rows": {
                        "type": "array",
                        "minItems": 1,
                        "items": {
                          "type": "object",
                          "required": ["spanish", "english"],
                          "properties": {
                            "spanish":       { "type": "string" },
                            "english":       { "type": "string" },
                            "pronunciation": { "type": "string" },
                            "cue":           { "type": "string" }
                          }
                        }
                      }
                    }
                  },
                  {
                    "type": "object",
                    "required": ["type", "markdown"],
                    "properties": {
                      "type":     { "const": "raw" },
                      "markdown": { "type": "string", "description": "Arbitrary Markdown including LCS container blocks like :::bingo-card or :::worksheet-grid." }
                    }
                  }
                ]
              }
            }
          }
        }
        """;
    }
}

public sealed record DraftingPrompt(string SystemPrompt, string ToolInputSchemaJson, string UserMessage);
