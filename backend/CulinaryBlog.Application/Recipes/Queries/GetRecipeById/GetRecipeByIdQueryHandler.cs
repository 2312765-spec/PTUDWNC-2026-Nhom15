using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipeById;

/// <summary>
/// D75 — không tồn tại / đã xóa mềm → 404. Published: ai cũng xem. Draft/Archived: chỉ tác giả hoặc Admin,
/// còn lại 403 RECIPE_FORBIDDEN (giống FR-RCP-002 A2, NFR-SEC-006).
/// </summary>
public sealed class GetRecipeByIdQueryHandler(
    IRecipeRepository recipeRepository,
    IIdentityService identityService,
    ICurrentUser currentUser)
    : IRequestHandler<GetRecipeByIdQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(GetRecipeByIdQuery request, CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetByIdDetailedAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

        var canView = recipe.Status == RecipeStatus.Published
            || (currentUser.IsAuthenticated && (currentUser.IsAdmin || recipe.AuthorId == currentUser.UserId));
        if (!canView)
        {
            throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền xem công thức này.");
        }

        var author = await identityService.GetUserByIdAsync(recipe.AuthorId, cancellationToken);
        return RecipeDetailDtoMapper.ToDetailDto(recipe, author);
    }
}
