using MediatR;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>FR-JOB-002/D42/D44 — Hangfire job chỉ chuyển tiếp sang MediatR command.</summary>
public sealed class ResizeRecipeImageJob(ISender sender)
{
    public ISender Sender { get; } = sender;

    public Task ExecuteAsync(Guid recipeId, Guid imageId, CancellationToken ct) => throw new NotImplementedException();
}
