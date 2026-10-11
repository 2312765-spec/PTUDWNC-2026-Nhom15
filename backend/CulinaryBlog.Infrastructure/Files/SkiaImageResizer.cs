using CulinaryBlog.Application.Common.Interfaces;
using SkiaSharp;

namespace CulinaryBlog.Infrastructure.Files;

/// <summary>
/// FR-JOB-002 — D40 (thumbnail crop 300×300, medium fit 800×600, không upscale, auto-orient rồi bỏ
/// metadata, giới hạn điểm ảnh) và D41 (đầu ra WebP; định dạng Skia không decode được như AVIF → null).
/// Thư viện: SkiaSharp (D46, ADR-0004).
/// </summary>
public sealed class SkiaImageResizer(long maxPixels = SkiaImageResizer.DefaultMaxPixels) : IImageResizer
{
    /// <summary>D40 — 40 megapixel: đủ cho ảnh điện thoại, chặn decompression bomb.</summary>
    public const long DefaultMaxPixels = 40_000_000;

    private const int MediumWidth = 800;
    private const int MediumHeight = 600;
    private const int ThumbnailSide = 300;
    private const int WebpQuality = 80;

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public long MaxPixels { get; } = maxPixels;

    public ResizedImageSet? Resize(Stream original)
    {
        using var buffer = new MemoryStream();
        original.CopyTo(buffer);
        using var data = SKData.CreateCopy(buffer.ToArray());

        // SKCodec chỉ đọc header (chưa decode pixel) → từ chối định dạng lạ và ảnh quá lớn mà không tốn RAM.
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return null;
        }

        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            return null;
        }

        using var decoded = Decode(codec);
        using var oriented = codec.EncodedOrigin == SKEncodedOrigin.TopLeft ? null : AutoOrient(decoded, codec.EncodedOrigin);
        var image = oriented ?? decoded;

        return new ResizedImageSet(Medium: ToMedium(image), Thumbnail: ToThumbnail(image));
    }

    /// <summary>
    /// Decode sang sRGB: màu đúng khi ảnh gốc mang ICC profile khác, và ảnh đầu ra không cần nhúng ICC.
    /// EXIF/XMP không bao giờ được Skia chép sang ảnh mã hóa lại → metadata bị bỏ (D40).
    /// </summary>
    private static SKImage Decode(SKCodec codec)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new InvalidOperationException($"Không decode được ảnh: {result}.");
        }

        bitmap.SetImmutable();
        var image = SKImage.FromBitmap(bitmap);
        bitmap.Dispose();
        return image;
    }

    /// <summary>D40 — xoay/lật theo EXIF Orientation để ảnh phái sinh hiển thị đúng chiều.</summary>
    private static SKImage AutoOrient(SKImage source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // Ma trận ánh xạ tọa độ ảnh lưu → tọa độ hiển thị (bảng EXIF Orientation 2–8).
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

        var width = swapsAxes ? source.Height : source.Width;
        var height = swapsAxes ? source.Width : source.Height;
        using var surface = CreateSurface(width, height);
        surface.Canvas.SetMatrix(matrix);
        surface.Canvas.DrawImage(source, 0, 0);
        return surface.Snapshot();
    }

    /// <summary>Thu cho lọt khung 800×600, giữ tỉ lệ, không phóng to.</summary>
    private static byte[] ToMedium(SKImage image)
    {
        var scale = Math.Min(1d, Math.Min((double)MediumWidth / image.Width, (double)MediumHeight / image.Height));
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        return Render(image, SKRect.Create(image.Width, image.Height), width, height);
    }

    /// <summary>Crop vuông giữa ảnh; cạnh ngắn nhỏ hơn 300 thì giữ cạnh ngắn, không phóng to.</summary>
    private static byte[] ToThumbnail(SKImage image)
    {
        var shortSide = Math.Min(image.Width, image.Height);
        var crop = SKRect.Create((image.Width - shortSide) / 2f, (image.Height - shortSide) / 2f, shortSide, shortSide);
        var side = Math.Min(ThumbnailSide, shortSide);
        return Render(image, crop, side, side);
    }

    private static byte[] Render(SKImage source, SKRect sourceRect, int width, int height)
    {
        using var surface = CreateSurface(width, height);
        using var paint = new SKPaint();
        surface.Canvas.DrawImage(source, sourceRect, SKRect.Create(width, height), Sampling, paint);
        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Webp, WebpQuality);
        return encoded.ToArray();
    }

    /// <summary>Surface không gắn color space → WebP đầu ra không nhúng ICC profile (D40).</summary>
    private static SKSurface CreateSurface(int width, int height) =>
        SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
        ?? throw new InvalidOperationException($"Không tạo được surface {width}×{height}.");
}
