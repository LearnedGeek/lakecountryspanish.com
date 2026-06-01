namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// A single Spanish vocabulary term — the building block for generated
/// worksheets (scavenger hunts, bingo cards, flashcards, matching pages).
/// Terms are tagged by theme so a generator can pull "all outdoor words"
/// or "all kitchen words" without hardcoding lists.
///
/// The image is stored as a <see cref="MediaAsset"/> reference rather than
/// a raw path so it participates in the existing media library (dedup by
/// hash, license tracking, AI-prompt regeneration). MediaAssetId is nullable
/// because terms can be seeded first and have their images generated later.
/// </summary>
public class VocabTerm
{
    public int Id { get; set; }

    /// <summary>
    /// The noun by itself, e.g. "árbol", "flor", "pájaro". Stored without
    /// the article so the same row can render as "el árbol" in a list or
    /// just "árbol" on a flashcard.
    /// </summary>
    public string Spanish { get; set; } = string.Empty;

    /// <summary>
    /// Definite article — "el", "la", "los", "las". Kept separate from the
    /// noun so renderers can show or hide it depending on the worksheet type.
    /// </summary>
    public string Article { get; set; } = string.Empty;

    /// <summary>
    /// English gloss for the answer key and English-only worksheets,
    /// e.g. "tree", "flower", "bird".
    /// </summary>
    public string English { get; set; } = string.Empty;

    /// <summary>
    /// Theme tag for filtering, e.g. "naturaleza", "cocina", "familia",
    /// "colores". Lowercase, no spaces — used as a slug.
    /// </summary>
    public string Theme { get; set; } = string.Empty;

    /// <summary>
    /// FK to the image used on worksheets. Nullable so terms can be seeded
    /// before their images are generated; the admin "generate missing images"
    /// action fills these in via Replicate.
    /// </summary>
    public int? MediaAssetId { get; set; }
    public virtual MediaAsset? MediaAsset { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
