namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// HỢP ĐỒNG CHUNG — chủ sở hữu: A. Ranh giới enqueue việc bất đồng bộ (Hangfire ở Infrastructure).
/// Application không được biết Hangfire.Core — chỉ gọi qua đây.
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>FR-AUTH-001 bước 11 → FR-JOB-001: fire-and-forget, không chờ kết quả.</summary>
    void EnqueueWelcomeEmail(string email, string displayName);
}
