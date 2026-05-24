using LakeCountrySpanish.Web.Services.Curriculum.Blocks;

namespace LakeCountrySpanish.Web.Models.ViewModels;

/// <summary>Top-level view model for the block editor list on the Day form.</summary>
public sealed class BlockListViewModel
{
    public int DayId { get; init; }
    public IReadOnlyList<Block> Blocks { get; init; } = Array.Empty<Block>();
}

/// <summary>Per-block envelope so partials know both their parent Day and the
/// block they render.</summary>
public sealed class BlockItemViewModel
{
    public int DayId { get; init; }
    public Block Block { get; init; } = null!;

    /// <summary>True when the block is in edit mode (form rendered instead of summary).</summary>
    public bool IsEditing { get; init; }
}
