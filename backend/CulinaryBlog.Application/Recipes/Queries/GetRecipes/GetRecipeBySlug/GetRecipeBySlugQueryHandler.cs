using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;

public sealed class GetRecipeBySlugQueryHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetRecipeBySlugQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(GetRecipeBySlugQuery request, CancellationToken cancellationToken)
    {
        // 1. Eager loading Recipe theo Slug
        var recipe = await recipeRepository.GetBySlugDetailedAsync(request.Slug, cancellationToken);

        // 2. Không tìm thấy hoặc đã soft-delete -> 404
        if (recipe is null || recipe.IsDeleted)
        {
            throw new NotFoundException(ErrorCodes.RecipeNotFound, $"Không tìm thấy công thức với slug '{request.Slug}'.");
        }

        // 3. Phân quyền: Draft / Archived chỉ cho chủ sở hữu hoặc Admin xem
        if (recipe.Status != RecipeStatus.Published)
        {
            if (!currentUser.IsAuthenticated)
            {
                throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền xem công thức này.");
            }

            var isOwner = recipe.AuthorId == currentUser.UserId;
            if (!isOwner && !currentUser.IsAdmin)
            {
                throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền xem công thức này.");
            }
        }

        // 4. Map sang RecipeDetailDto
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
                recipe.Category?.Slug ?? string.Empty
            ),
            new RecipeAuthorDto(
                recipe.AuthorId,
                "Tác giả",
                null
            ),
            recipe.Nutrition is null ? null : new RecipeNutritionDto(
                recipe.Nutrition.Calories,
                recipe.Nutrition.Protein,
                recipe.Nutrition.Carbohydrates,
                recipe.Nutrition.Fat,
                recipe.Nutrition.Fiber,
                recipe.Nutrition.Sodium
            ),
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
                .ToList()
        );
    }
}