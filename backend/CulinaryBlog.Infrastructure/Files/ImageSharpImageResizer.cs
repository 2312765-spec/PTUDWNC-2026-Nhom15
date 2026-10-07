using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.Infrastructure.Files;

/// <summary>FR-JOB-002 — D40 (crop/fit, không upscale, bỏ EXIF, giới hạn điểm ảnh), D41 (WebP, AVIF → null).</summary>
public sealed class ImageSharpImageResizer(long maxPixels = ImageSharpImageResizer.DefaultMaxPixels) : IImageResizer
{
    /// <summary>D40 — 40 megapixel: đủ cho ảnh điện thoại, chặn decompression bomb.</summary>
    public const long DefaultMaxPixels = 40_000_000;

    public long MaxPixels { get; } = maxPixels;

    public ResizedImageSet? Resize(Stream original) => throw new NotImplementedException();
}
