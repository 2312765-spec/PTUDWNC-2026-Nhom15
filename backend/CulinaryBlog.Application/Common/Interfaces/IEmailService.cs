using CulinaryBlog.Application.Common.Email;

namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>HỢP ĐỒNG CHUNG — chủ sở hữu: A. FR-JOB-001.</summary>
public interface IEmailService
{
    /// <summary>
    /// D57: chỉ gửi, không dựng nội dung. Lỗi SMTP phải ném ra để Hangfire retry (D42).
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
