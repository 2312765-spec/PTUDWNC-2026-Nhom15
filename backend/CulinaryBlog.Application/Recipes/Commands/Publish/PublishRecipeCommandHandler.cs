using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Observability;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands.Update;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Recipes.Commands.Publish;

/// <summary>
/// FR-RCP-005 — xuất bản / hủy xuất bản. Thứ tự: 404 → 403 → 409 (If-Match, nếu có) → D3 (400).
/// D77: khóa dòng recipe trong transaction — publish không chen được với lệnh xóa bước/nguyên liệu cuối cùng
/// (D73/D76), và quyền được kiểm tra lại ngay trên bản ghi đã khóa (permissions.md: double-check trước khi commit).
/// D39: chỉ đếm metric khi trạng thái thực sự đổi sang Published và SaveChanges thành công.
/// </summary>
public sealed class PublishRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IIdentityService identityService,
    RecipeAuthorizationHandler authorization,
    ICurrentUser currentUser,
    RecipeMetrics metrics,
    ILogger<PublishRecipeCommandHandler> logger)
    : IRequestHandler<PublishRecipeCommand, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(PublishRecipeCommand request, CancellationToken cancellationToken)
    {
        Recipe? recipe = null;
        var changed = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            // D71/D77: If-Match tùy chọn — có thì client đang thao tác trên bản đã cũ → 409.
            if (RowVersionHeader.TryParse(request.IfMatch, out var expected)
                && !expected.AsSpan().SequenceEqual(recipe.RowVersion))
            {
                throw new ConflictException(
                    ErrorCodes.RecipeConcurrencyConflict,
                    "Dữ liệu đã bị thay đổi bởi người dùng khác. Tải lại công thức rồi thử lại.");
            }

            changed = request.IsPublish ? recipe.Publish() : recipe.Unpublish();
            if (changed)
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
        }, cancellationToken);

        if (changed)
        {
            if (request.IsPublish)
            {
                metrics.RecordPublished();
            }

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
