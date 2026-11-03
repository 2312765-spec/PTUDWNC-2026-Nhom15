using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.SendWelcomeEmail;

/// <summary>
/// FR-JOB-001 (D12, D56, D57) — lệnh hệ thống do <c>WelcomeEmailJob</c> (Hangfire) gửi, không có
/// endpoint. <paramref name="SiteBaseUrl"/> là gốc site công khai (Seo:SiteBaseUrl), job đọc cấu hình rồi
/// truyền vào. Không implement ICacheInvalidator — không cache nào đổi.
/// </summary>
public sealed record SendWelcomeEmailCommand(string Email, string DisplayName, string SiteBaseUrl) : IRequest;
