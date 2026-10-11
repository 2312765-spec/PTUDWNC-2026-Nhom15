using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>
/// FR-JOB-002/D44 — lệnh hệ thống, chỉ Hangfire job (<c>ResizeRecipeImageJob</c>) gửi; không có endpoint.
/// </summary>
public sealed record GenerateRecipeImageVariantsCommand(Guid RecipeId, Guid ImageId) : IRequest, ICacheInvalidator
{
    /// <summary>D8 — chỉ gán khi thật sự ghi URL mới (no-op thì không xóa cache).</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
