using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;

/// <summary>
/// FR-RCP-002, NFR-SEC-006 — lấy dữ liệu (có cache) rồi mới kiểm tra quyền. Published: ai cũng xem
/// được. Draft/Archived: chỉ tác giả hoặc Admin, còn lại 403 RECIPE_FORBIDDEN (FR-RCP-002 A2).
/// Thứ tự này chặn việc khách đọc được bản nháp mà chủ sở hữu vừa xem (cache dùng chung recipe:{slug}).
/// </summary>
public sealed class GetRecipeBySlugQueryHandler(ISender sender, ICurrentUser currentUser)
    : IRequestHandler<GetRecipeBySlugQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(GetRecipeBySlugQuery request, CancellationToken cancellationToken)
    {
        var recipe = await sender.Send(new GetRecipeDetailQuery(request.Slug), cancellationToken);

        if (recipe.Status == (short)RecipeStatus.Published)
        {
            return recipe;
        }

        var canView = currentUser.IsAuthenticated
            && (currentUser.IsAdmin || recipe.Author.Id == currentUser.UserId);
        if (!canView)
        {
            throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền xem công thức này.");
        }

        return recipe;
    }
}
