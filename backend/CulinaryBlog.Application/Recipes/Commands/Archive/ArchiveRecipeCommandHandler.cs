using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands.Update;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Recipes.Commands.Archive;

/// <summary>
/// FR-RCP-006 — lưu trữ công thức. Thứ tự: 404 → 403 → 409 (If-Match, nếu có).
/// D79: khóa dòng recipe trong transaction (xếp hàng với lệnh sửa/publish/xóa đang chạy), quyền kiểm tra lại trên bản
/// ghi đã khóa; đã Archived → không SaveChanges (RowVersion giữ nguyên). Không có metric (D39 chỉ đếm publish).
/// </summary>
public sealed class ArchiveRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IIdentityService identityService,
    RecipeAuthorizationHandler authorization,
    ICurrentUser currentUser,
    ILogger<ArchiveRecipeCommandHandler> logger)
    : IRequestHandler<ArchiveRecipeCommand, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(ArchiveRecipeCommand request, CancellationToken cancellationToken)
    {
        Recipe? recipe = null;
        var changed = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            // D71/D79: If-Match tùy chọn — có thì client đang thao tác trên bản đã cũ → 409.
            if (RowVersionHeader.TryParse(request.IfMatch, out var expected)
                && !expected.AsSpan().SequenceEqual(recipe.RowVersion))
            {
                throw new ConflictException(
                    ErrorCodes.RecipeConcurrencyConflict,
                    "Dữ liệu đã bị thay đổi bởi người dùng khác. Tải lại công thức rồi thử lại.");
            }

            changed = recipe.Archive();
            if (changed)
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
        }, cancellationToken);

        if (changed)
        {
            // NFR-SEC-006 — audit trail cho lệnh ghi nhạy cảm.
            logger.LogInformation(
                "Recipe {RecipeId} chuyển sang {Status} bởi {UserId}",
                recipe!.Id, recipe.Status, currentUser.UserId);
        }

        request.TagsToInvalidate = ["recipes", $"recipe:{recipe!.Slug}"];

        var category = await categoryRepository.GetByIdAsync(recipe.CategoryId, cancellationToken);
        var author = await identityService.GetUserByIdAsync(recipe.AuthorId, cancellationToken);
        return RecipeDetailDtoMapper.ToDetailDto(recipe, author, category);
    }
}
