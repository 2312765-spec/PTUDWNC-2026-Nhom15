using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-AUTH-001 bước 11: BackgroundJob.Enqueue — fire-and-forget qua Hangfire.
/// Enqueue thẳng vào IEmailService (chưa có lớp WelcomeEmailJob riêng với retry
/// policy 1p/5p/30p tùy chỉnh — đó là phần còn lại của FR-JOB-001).
/// </summary>
public sealed class HangfireBackgroundJobService(IBackgroundJobClient backgroundJobClient) : IBackgroundJobService
{
    public void EnqueueWelcomeEmail(string email, string displayName) =>
        backgroundJobClient.Enqueue<IEmailService>(s => s.SendWelcomeEmailAsync(email, displayName, CancellationToken.None));

    /// <summary>FR-FILE-002: DeleteAsync đã idempotent (không throw nếu object không tồn tại) — an toàn để Hangfire retry.</summary>
    public void EnqueueDeleteImageFile(string fileUrl) =>
        backgroundJobClient.Enqueue<IFileStorageService>(s => s.DeleteAsync(fileUrl, CancellationToken.None));
}
