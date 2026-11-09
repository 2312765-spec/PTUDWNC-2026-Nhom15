namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>FR-JOB-002/D40/D41/D88 — ba phiên bản phái sinh, đã mã hóa WebP.</summary>
public sealed record ResizedImageSet(byte[] Medium, byte[] Thumbnail, byte[] Card);

/// <summary>
/// FR-JOB-002 — sinh ảnh medium (fit 800×600), thumbnail (crop 300×300) (D40) và card (crop 760×320 — D88) từ ảnh gốc,
/// đầu ra WebP (D41). Hiện thực ở Infrastructure (SkiaSharp — D46) — Application không biết thư viện ảnh.
/// </summary>
public interface IImageResizer
{
    public const string OutputContentType = "image/webp";

    /// <summary>
    /// Trả <c>null</c> khi không xử lý được và thử lại cũng vô ích: định dạng không decode được
    /// (AVIF — D41) hoặc ảnh vượt giới hạn điểm ảnh (chống decompression bomb — D40).
    /// </summary>
    ResizedImageSet? Resize(Stream original);
}
