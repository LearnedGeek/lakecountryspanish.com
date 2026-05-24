using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LakeCountrySpanish.Web.Services;

public sealed class ImageProcessingService : IImageProcessingService
{
    public string ComputeSha256(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexStringLower(hash);
    }

    public (int width, int height) GetDimensions(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var info = Image.Identify(stream);
        return (info.Width, info.Height);
    }

    public byte[] CreateThumbnailJpeg(byte[] sourceBytes, int maxWidth, int maxHeight)
    {
        using var input = new MemoryStream(sourceBytes);
        using var image = Image.Load(input);

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(maxWidth, maxHeight),
            Mode = ResizeMode.Max
        }));

        using var output = new MemoryStream();
        image.Save(output, new JpegEncoder { Quality = 82 });
        return output.ToArray();
    }
}
