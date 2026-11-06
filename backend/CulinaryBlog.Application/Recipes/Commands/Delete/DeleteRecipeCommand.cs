using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Delete;

/// <summary>
/// FR-RCP-007 — DELETE /api/v1/recipes/{id}. D1: soft delete, không cascade, không xóa file MinIO.
/// <paramref name="IfMatch"/> TÙY CHỌN (D78, giống D77): có thì phải khớp RowVersion hiện tại.
/// </summary>
public sealed record DeleteRecipeCommand(Guid RecipeId, string? IfMatch = null) : IRequest, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
