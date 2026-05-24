namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Image manipulation primitives used by the media library: hashing for
/// deduplication, dimension probe, and thumbnail generation. Wraps
/// SixLabors.ImageSharp so callers don't take a direct dependency on it.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>SHA-256 of the byte sequence, lowercase hex.</summary>
    string ComputeSha256(ReadOnlySpan<byte> bytes);

    /// <summary>Reads width/height without fully decoding the image.</summary>
    (int width, int height) GetDimensions(byte[] bytes);

    /// <summary>
    /// Returns a JPEG-encoded thumbnail constrained to fit within
    /// (maxWidth × maxHeight) while preserving aspect ratio. Quality is fixed
    /// at 82 — good balance for kid-friendly artwork in browser previews.
    /// </summary>
    byte[] CreateThumbnailJpeg(byte[] sourceBytes, int maxWidth, int maxHeight);
}
