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
    public const string DeletedAuthorDisplayName = RecipeDetailDtoMapper.DeletedAuthorDisplayName;

    public async Task<RecipeDetailDto> Handle(GetRecipeDetailQuery request, CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetBySlugDetailedAsync(request.Slug, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, $"Không tìm thấy công thức với slug '{request.Slug}'.");

        var author = await identityService.GetUserByIdAsync(recipe.AuthorId, cancellationToken);

        return RecipeDetailDtoMapper.ToDetailDto(recipe, author);
    }
}
