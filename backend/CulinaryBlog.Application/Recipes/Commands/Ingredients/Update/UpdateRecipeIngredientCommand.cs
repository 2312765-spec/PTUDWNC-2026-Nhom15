using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Ingredients;

/// <summary>
/// FR-RCP-009 — PUT /api/v1/recipes/{id}/ingredients/{ingredientId}. Thay nội dung nguyên liệu (name bắt buộc;
/// quantity/unit/notes null = xóa). <paramref name="OrderIndex"/> có giá trị thì chuyển tới vị trí đó và đánh lại
/// toàn bộ (D76). Trả về mọi nguyên liệu của recipe theo thứ tự mới.
/// </summary>
public sealed record UpdateRecipeIngredientCommand(
    Guid RecipeId,
    Guid IngredientId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex) : IRequest<IReadOnlyList<RecipeIngredientDto>>, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
