using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Recipes.DTOs;

/// <summary>
/// Map Recipe (đã nạp Category, Steps, Ingredients, Images) sang <see cref="RecipeDetailDto"/> — dùng chung
/// cho FR-RCP-002 (xem chi tiết) và các lệnh ghi FR-RCP-003/004 (trả về bản mới nhất). Không trả entity ra API.
/// </summary>
public static class RecipeDetailDtoMapper
{
    /// <summary>Tác giả đã bị xóa khỏi hệ thống — công thức vẫn hiển thị được.</summary>
    public const string DeletedAuthorDisplayName = "Tài khoản đã xóa";

    public static RecipeDetailDto ToDetailDto(Recipe recipe, AuthenticatedUser? author, Category? category = null)
    {
        category ??= recipe.Category;

        return new RecipeDetailDto(
            recipe.Id,
            recipe.Title,
            recipe.Slug,
            recipe.Description,
            recipe.Instructions,
            (short)recipe.Difficulty,
            (short)recipe.Status,
            recipe.PrepTime,
            recipe.CookTime,
            recipe.Servings,
            recipe.PublishedAt,
            recipe.CreatedAt,
            recipe.UpdatedAt,
            new RecipeCategoryDto(
                category?.Id ?? recipe.CategoryId,
                category?.Name ?? string.Empty,
                category?.Slug ?? string.Empty),
            new RecipeAuthorDto(
                recipe.AuthorId,
                author?.DisplayName ?? DeletedAuthorDisplayName,
                author?.AvatarUrl),
            // Owned type: EF trả null khi mọi cột Nutrition_* đều null (công thức không khai báo dinh dưỡng).
            recipe.Nutrition is null ? null : new RecipeNutritionDto(
                recipe.Nutrition.Calories,
                recipe.Nutrition.Protein,
                recipe.Nutrition.Carbohydrates,
                recipe.Nutrition.Fat,
                recipe.Nutrition.Fiber,
                recipe.Nutrition.Sodium),
            recipe.Ingredients
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.OrderIndex)
                .Select(i => new RecipeIngredientDto(i.Id, i.Name, i.Quantity, i.Unit, i.Notes, i.OrderIndex))
                .ToList(),
            ToStepDtos(recipe),
            recipe.Images
                .Where(img => !img.IsDeleted)
                .OrderBy(img => img.OrderIndex)
                .Select(img => new RecipeImageDto(img.Id, img.OriginalUrl, img.AltText, img.IsPrimary, img.OrderIndex))
                .ToList())
        {
            RowVersion = Convert.ToBase64String(recipe.RowVersion),
        };
    }

    /// <summary>Danh sách bước theo StepNumber tăng dần (FR-RCP-010 trả về sau khi sửa/đổi vị trí).</summary>
    public static IReadOnlyList<RecipeStepDto> ToStepDtos(Recipe recipe) =>
        recipe.Steps
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.StepNumber)
            .Select(ToStepDto)
            .ToList();

    public static RecipeStepDto ToStepDto(RecipeStep s) =>
        new(s.Id, s.StepNumber, s.Title, s.Description, s.TimerMinutes, s.ImageUrl);
}
