namespace LakeCountrySpanish.Web.Services.Curriculum.Blocks;

/// <summary>
/// Compiles a typed block list (authoritative form) into the LCS-flavored
/// Markdown the existing renderer consumes. One-way transform — there is no
/// reverse parse step. Markdown is a derived/cached representation.
/// </summary>
public interface IBlockCompiler
{
    /// <summary>Serialize the block list as JSON for the BodyBlocksJson column.</summary>
    string Serialize(IReadOnlyList<Block> blocks);

    /// <summary>Deserialize the JSON back to a typed block list. Empty JSON returns an empty list.</summary>
    IReadOnlyList<Block> Deserialize(string? json);

    /// <summary>Emit the Markdown body the existing renderer pipeline expects.</summary>
    string Compile(IReadOnlyList<Block> blocks);
}
