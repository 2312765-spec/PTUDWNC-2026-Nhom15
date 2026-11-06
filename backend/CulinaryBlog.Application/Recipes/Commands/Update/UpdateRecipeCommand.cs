using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Update;

/// <summary>
/// FR-RCP-004 — PUT /api/v1/recipes/{id}. Thay toàn bộ thông tin cơ bản (D69), slug không đổi (D13).
/// <paramref name="IfMatch"/> là giá trị thô của header <c>If-Match</c> (D71) — validator kiểm tra định dạng,
/// handler so với RowVersion trong DB.
/// </summary>
public sealed record UpdateRecipeCommand(
    Guid Id,
    string? IfMatch,
    string Title,
    string Description,
    Guid CategoryId,
    int PrepTime,
    int CookTime,
    int Servings,
    string Difficulty,
    string? Instructions,
    RecipeNutritionInput? Nutrition)
    : IRequest<RecipeDetailDto>, ICacheInvalidator, IRecipeDetailsInput
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
