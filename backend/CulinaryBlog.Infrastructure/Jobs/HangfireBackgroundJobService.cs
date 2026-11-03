using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>Ranh giới enqueue job Hangfire (fire-and-forget) — Application chỉ thấy IBackgroundJobService.</summary>
public sealed class HangfireBackgroundJobService(IBackgroundJobClient backgroundJobClient) : IBackgroundJobService
{
    /// <summary>FR-JOB-001 — retry theo D42 khai báo trên <see cref="WelcomeEmailJob"/>.</summary>
    public void EnqueueWelcomeEmail(string email, string displayName) =>
        backgroundJobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(email, displayName, CancellationToken.None));

    /// <summary>FR-FILE-002: DeleteAsync đã idempotent (không throw nếu object không tồn tại) — an toàn để Hangfire retry.</summary>
    public void EnqueueDeleteImageFile(string fileUrl) =>
        backgroundJobClient.Enqueue<IFileStorageService>(s => s.DeleteAsync(fileUrl, CancellationToken.None));

    /// <summary>FR-JOB-002/D44 — retry theo D42 khai báo trên <see cref="ResizeRecipeImageJob"/>.</summary>
    public void EnqueueGenerateImageVariants(Guid recipeId, Guid imageId) =>
        backgroundJobClient.Enqueue<ResizeRecipeImageJob>(j => j.ExecuteAsync(recipeId, imageId, CancellationToken.None));
}
