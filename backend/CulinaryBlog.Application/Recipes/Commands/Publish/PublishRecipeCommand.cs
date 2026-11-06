using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Publish;

/// <summary>
/// FR-RCP-005 — PATCH /api/v1/recipes/{id}/publish (<paramref name="IsPublish"/> = true) hoặc /unpublish (false).
/// <paramref name="IfMatch"/> TÙY CHỌN (D77): có thì phải khớp RowVersion hiện tại, vắng thì bỏ qua kiểm tra.
/// </summary>
public sealed record PublishRecipeCommand(Guid RecipeId, bool IsPublish, string? IfMatch = null)
    : IRequest<RecipeDetailDto>, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
