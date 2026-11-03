using CulinaryBlog.Application.Auth.Commands.SendWelcomeEmail;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-JOB-001 — Hangfire fire-and-forget sau đăng ký (D56). Retry 3 lần chờ 1 / 5 / 30 phút, hết lượt
/// → Failed (D42); mỗi lần lỗi LoggingBehavior log Error. Job chỉ chuyển tiếp sang MediatR (D44, D57),
/// gốc URL cho link trong email lấy từ Seo:SiteBaseUrl.
/// </summary>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class WelcomeEmailJob(ISender sender, IConfiguration configuration)
{
    /// <summary>Hangfire thay <paramref name="ct"/> bằng token huỷ của server khi chạy job.</summary>
    public Task ExecuteAsync(string email, string displayName, CancellationToken ct) =>
        sender.Send(
            new SendWelcomeEmailCommand(email, displayName, configuration["Seo:SiteBaseUrl"] ?? string.Empty),
            ct);
}
