using CulinaryBlog.Application.Common.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace CulinaryBlog.Infrastructure.Files;

/// <summary>
/// FR-JOB-002 — D40 (thumbnail crop 300×300, medium fit 800×600, không upscale, auto-orient rồi bỏ
/// metadata, giới hạn điểm ảnh) và D41 (đầu ra WebP; định dạng ImageSharp không decode được như
/// AVIF → null).
/// </summary>
public sealed class ImageSharpImageResizer(long maxPixels = ImageSharpImageResizer.DefaultMaxPixels) : IImageResizer
{
    /// <summary>D40 — 40 megapixel: đủ cho ảnh điện thoại, chặn decompression bomb.</summary>
    public const long DefaultMaxPixels = 40_000_000;

    private static readonly Size MediumBox = new(800, 600);
    private const int ThumbnailSide = 300;

    private static readonly WebpEncoder Encoder = new() { Quality = 80 };

    public long MaxPixels { get; } = maxPixels;

    public ResizedImageSet? Resize(Stream original)
    {
        // Đọc header trước (không decode pixel) để từ chối định dạng lạ và ảnh quá lớn mà không tốn RAM.
        ImageInfo info;
        try
        {
            info = Image.Identify(original);
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }

        if ((long)info.Width * info.Height > MaxPixels)
        {
            return null;
        }

        original.Position = 0;
        using var image = Image.Load(original);
        image.Mutate(x => x.AutoOrient());
        StripMetadata(image);

        return new ResizedImageSet(Medium: Encode(image, ToMedium), Thumbnail: Encode(image, ToThumbnail));
    }

    /// <summary>Thu cho lọt khung, giữ tỉ lệ, không phóng to (ResizeMode.Max chỉ thu nhỏ khi cần).</summary>
    private static void ToMedium(IImageProcessingContext x)
    {
        var size = x.GetCurrentSize();
        if (size.Width <= MediumBox.Width && size.Height <= MediumBox.Height)
        {
            return;
        }

        x.Resize(new ResizeOptions { Size = MediumBox, Mode = ResizeMode.Max });
    }

    /// <summary>Crop vuông giữa ảnh; cạnh ngắn nhỏ hơn 300 thì giữ cạnh ngắn, không phóng to.</summary>
    private static void ToThumbnail(IImageProcessingContext x)
    {
        var size = x.GetCurrentSize();
        var side = Math.Min(ThumbnailSide, Math.Min(size.Width, size.Height));
        x.Resize(new ResizeOptions { Size = new Size(side, side), Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center });
    }

    private static byte[] Encode(Image source, Action<IImageProcessingContext> transform)
    {
        using var clone = source.Clone(transform);
        using var output = new MemoryStream();
        clone.Save(output, Encoder);
        return output.ToArray();
    }

    /// <summary>D40 — bỏ EXIF (có thể chứa GPS), XMP, IPTC sau khi đã dùng Orientation để xoay.</summary>
    private static void StripMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IptcProfile = null;
        foreach (var frame in image.Frames)
        {
            frame.Metadata.ExifProfile = null;
            frame.Metadata.XmpProfile = null;
            frame.Metadata.IptcProfile = null;
        }
    }
}
