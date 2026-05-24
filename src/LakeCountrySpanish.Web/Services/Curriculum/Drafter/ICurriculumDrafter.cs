using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services.Curriculum.Blocks;

namespace LakeCountrySpanish.Web.Services.Curriculum.Drafter;

/// <summary>
/// Drafts a structured LCS lesson from a teacher's plain-English brief.
/// Wraps Anthropic's Messages API with the LCS component vocabulary baked
/// into the system prompt and forced tool-use to guarantee a valid block
/// JSON response. See <c>docs/curriculum-system/ai-assisted-authoring.md</c>
/// for the architecture and decision history.
/// </summary>
public interface ICurriculumDrafter
{
    /// <summary>True when the Anthropic API key is configured.</summary>
    bool IsAvailable { get; }

    Task<DraftLessonResult> DraftAsync(DraftLessonRequest request, CancellationToken ct = default);
}

/// <summary>What Karen tells us when she clicks "Draft this".</summary>
public sealed class DraftLessonRequest
{
    /// <summary>Free-form description of the lesson she wants. The whole UX hinges on this field.</summary>
    public string Brief { get; init; } = string.Empty;

    /// <summary>
    /// Optional parent Unit. If unset, the drafter picks the closest match from
    /// available Units (or returns a flag asking which Unit to attach to).
    /// </summary>
    public int? UnitId { get; init; }

    /// <summary>
    /// Optional target grade band. If unset, the drafter infers from the brief.
    /// </summary>
    public GradeBand? GradeBand { get; init; }
}

/// <summary>The drafter's structured output, mapped onto our Day + block types.</summary>
public sealed class DraftLessonResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public string Title { get; init; } = string.Empty;
    public string Theme { get; init; } = string.Empty;
    public int UnitId { get; init; }
    public GradeBand GradeBand { get; init; }
    public int EstimatedDurationMinutes { get; init; } = 25;

    /// <summary>WI standard codes the drafter selected (e.g. "WL.PS.3.c.n1").</summary>
    public IReadOnlyList<string> WiStandardCodes { get; init; } = Array.Empty<string>();

    /// <summary>The lesson body as our internal block list.</summary>
    public IReadOnlyList<Block> Blocks { get; init; } = Array.Empty<Block>();

    /// <summary>Diagnostic info — the full prompt + raw response for audit/debug.</summary>
    public string? SystemPrompt { get; init; }
    public string? RawToolInputJson { get; init; }
}
