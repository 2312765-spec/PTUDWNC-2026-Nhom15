using System.Collections.Concurrent;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.IntegrationTests.Recipes.Support;

/// <summary>
/// Ghi lại các lần enqueue thay cho Hangfire thật — môi trường <c>Testing</c> không chạy Hangfire
/// server (D45), nên test kiểm tra "đã enqueue đúng việc" rồi tự gọi command/job khi cần.
/// </summary>
public sealed class RecordingBackgroundJobService : IBackgroundJobService
{
    public ConcurrentBag<string> DeleteImageFileUrls { get; } = [];

    public ConcurrentBag<(Guid RecipeId, Guid ImageId)> GenerateImageVariants { get; } = [];

    /// <summary>FR-JOB-001 — các lần enqueue welcome email (D56).</summary>
    public ConcurrentBag<(string Email, string DisplayName)> WelcomeEmails { get; } = [];

    /// <summary>FR-JOB-001/D55 — giả lập Hangfire storage lỗi lúc enqueue welcome email.</summary>
    public bool ThrowOnWelcomeEmail { get; set; }

    public void EnqueueWelcomeEmail(string email, string displayName)
    {
        if (ThrowOnWelcomeEmail)
        {
            throw new InvalidOperationException("Giả lập Hangfire storage không khả dụng.");
        }

        WelcomeEmails.Add((email, displayName));
    }

    public void EnqueueDeleteImageFile(string fileUrl) => DeleteImageFileUrls.Add(fileUrl);

    public void EnqueueGenerateImageVariants(Guid recipeId, Guid imageId) => GenerateImageVariants.Add((recipeId, imageId));
}
