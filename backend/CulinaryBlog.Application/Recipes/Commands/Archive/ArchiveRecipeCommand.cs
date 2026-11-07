using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Archive;

/// <summary>
/// FR-RCP-006 — PATCH /api/v1/recipes/{id}/archive. Draft/Published → Archived (D79).
/// <paramref name="IfMatch"/> TÙY CHỌN (D79, giống D77): có thì phải khớp RowVersion hiện tại, vắng thì bỏ qua kiểm tra.
/// </summary>
public sealed record ArchiveRecipeCommand(Guid RecipeId, string? IfMatch = null)
    : IRequest<RecipeDetailDto>, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
