using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Ingredients;

/// <summary>
/// FR-RCP-009 — POST /api/v1/recipes/{id}/ingredients. D7: <c>quantity</c>/<c>unit</c> nullable.
/// D76: <paramref name="OrderIndex"/> vắng = xếp cuối; có giá trị (0..số nguyên liệu) = chèn vào vị trí đó.
/// </summary>
public sealed record AddRecipeIngredientCommand(
    Guid RecipeId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex) : IRequest<RecipeIngredientDto>, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
