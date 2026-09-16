namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>HỢP ĐỒNG CHUNG — chủ sở hữu: A. FR-JOB-001.</summary>
public interface IEmailService
{
    /// <summary>D12: email chào mừng KHÔNG có link kích hoạt — xác nhận email ngoài scope v1.</summary>
    Task SendWelcomeEmailAsync(string toEmail, string displayName, CancellationToken ct = default);
}
