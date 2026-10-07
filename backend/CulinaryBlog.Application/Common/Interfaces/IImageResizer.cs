namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>FR-JOB-002/D40/D41 — hai phiên bản phái sinh, đã mã hóa WebP.</summary>
public sealed record ResizedImageSet(byte[] Medium, byte[] Thumbnail);

/// <summary>
/// FR-JOB-002 — sinh ảnh medium (fit 800×600) và thumbnail (crop 300×300) từ ảnh gốc (D40),
/// đầu ra WebP (D41). Hiện thực ở Infrastructure (ImageSharp) — Application không biết thư viện ảnh.
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
