namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// HỢP ĐỒNG CHUNG — chủ sở hữu: D. Chốt tuần 1.
/// Abstraction cho object storage (MinIO ↔ AWS S3 ↔ local filesystem) — FR-FILE-001/002.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Trả về URL công khai. Tên file sinh bằng GUID (chống path traversal, NFR-SEC-004).
    /// Kiểm tra size TRƯỚC khi đọc stream, rồi MIME, rồi magic bytes (CONS-007).
    /// </summary>
    Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default);

    /// <summary>Idempotent — object không tồn tại thì không throw (FR-FILE-002).</summary>
    Task DeleteAsync(string fileUrl, CancellationToken ct = default);
}
