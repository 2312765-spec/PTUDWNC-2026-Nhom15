namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// HỢP ĐỒNG CHUNG — chủ sở hữu: A. Ranh giới enqueue việc bất đồng bộ (Hangfire ở Infrastructure).
/// Application không được biết Hangfire.Core — chỉ gọi qua đây.
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>FR-AUTH-001 bước 11 → FR-JOB-001: fire-and-forget, không chờ kết quả.</summary>
    void EnqueueWelcomeEmail(string email, string displayName);

    /// <summary>FR-FILE-002 — xóa file MinIO bất đồng bộ khi xóa một ảnh cụ thể (D1: không dùng khi xóa cả recipe).</summary>
    void EnqueueDeleteImageFile(string fileUrl);
}
