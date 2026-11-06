using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Create;

/// <summary>
/// FR-RCP-003 — POST /api/v1/recipes. Recipe mới luôn là Draft, slug sinh từ title (D10, D72).
/// Steps/ingredients/nutrition tùy chọn, gửi kèm luôn hoặc thêm sau qua FR-RCP-009/010.
/// Tên trường theo SRS Chương 8 (prepTime/cookTime — D69).
/// </summary>
public sealed record CreateRecipeCommand(
    string Title,
    string Description,
    Guid CategoryId,
    int PrepTime,
    int CookTime,
    int Servings,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(StringOrNumberJsonConverter))] string Difficulty,
    string? Instructions = null,
    RecipeNutritionInput? Nutrition = null,
    IReadOnlyList<RecipeStepInput>? Steps = null,
    IReadOnlyList<RecipeIngredientInput>? Ingredients = null)
    : IRequest<RecipeDetailDto>, ICacheInvalidator, IRecipeDetailsInput
{
    /// <summary>D8 — recipe mới chưa có cache chi tiết, chỉ cần xóa các danh sách.</summary>
    public IReadOnlyList<string> TagsToInvalidate => ["recipes"];
}
