using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;

/// <summary>
/// FR-RCP-002 — tải công thức kèm category, steps, ingredients, images (một query, không N+1) và
/// hồ sơ tác giả (D5). Không tồn tại hoặc đã soft-delete (D1) → 404 RECIPE_NOT_FOUND.
/// </summary>
public sealed class GetRecipeDetailQueryHandler(
    IRecipeRepository recipeRepository,
    IIdentityService identityService)
    : IRequestHandler<GetRecipeDetailQuery, RecipeDetailDto>
{
    /// <summary>Tác giả đã bị xóa khỏi hệ thống — công thức vẫn hiển thị được.</summary>
    public const string DeletedAuthorDisplayName = "Tài khoản đã xóa";

    public async Task<RecipeDetailDto> Handle(GetRecipeDetailQuery request, CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetBySlugDetailedAsync(request.Slug, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, $"Không tìm thấy công thức với slug '{request.Slug}'.");

        var author = await identityService.GetUserByIdAsync(recipe.AuthorId, cancellationToken);

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
                recipe.Category?.Id ?? recipe.CategoryId,
                recipe.Category?.Name ?? string.Empty,
                recipe.Category?.Slug ?? string.Empty),
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
            recipe.Steps
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.StepNumber)
                .Select(s => new RecipeStepDto(s.Id, s.StepNumber, s.Title, s.Description, s.TimerMinutes, s.ImageUrl))
                .ToList(),
            recipe.Images
                .Where(img => !img.IsDeleted)
                .OrderBy(img => img.OrderIndex)
                .Select(img => new RecipeImageDto(img.Id, img.OriginalUrl, img.AltText, img.IsPrimary, img.OrderIndex))
                .ToList());
    }
}
