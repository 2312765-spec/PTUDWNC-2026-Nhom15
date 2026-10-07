using CulinaryBlog.Application.Recipes.Commands.Images;
using Hangfire;
using MediatR;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-JOB-002 — Hangfire job chỉ chuyển tiếp sang MediatR command (D44), để logic nằm ở Application
/// và đi qua pipeline (Logging, Validation, CacheInvalidation — D8).
/// D42: retry 3 lần, chờ 1 / 5 / 30 phút; hết lượt → Failed (xem ở /hangfire).
/// </summary>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class ResizeRecipeImageJob(ISender sender)
{
    /// <summary>Hangfire thay <paramref name="ct"/> bằng token huỷ của server khi chạy job.</summary>
    public Task ExecuteAsync(Guid recipeId, Guid imageId, CancellationToken ct) =>
        sender.Send(new GenerateRecipeImageVariantsCommand(recipeId, imageId), ct);
}
