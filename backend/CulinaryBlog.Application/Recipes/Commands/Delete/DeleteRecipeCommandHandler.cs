using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands.Update;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Recipes.Commands.Delete;

/// <summary>
/// FR-RCP-007 — xóa công thức. D1: chỉ đặt <c>IsDeleted = true</c> qua <see cref="IRecipeRepository.Delete"/>;
/// steps/ingredients/images giữ nguyên, KHÔNG enqueue job xóa file MinIO (recipe còn khôi phục được bằng DB).
/// Thứ tự: 404 → 403 → 409 (If-Match, nếu có). D78: khóa dòng recipe — lệnh xóa xếp hàng sau lệnh sửa
/// bước/nguyên liệu/publish đang chạy, quyền kiểm tra trên bản ghi đã khóa ngay trước khi ghi.
/// </summary>
public sealed class DeleteRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    IUnitOfWork unitOfWork,
    RecipeAuthorizationHandler authorization,
    ICurrentUser currentUser,
    ILogger<DeleteRecipeCommandHandler> logger)
    : IRequestHandler<DeleteRecipeCommand>
{
    public async Task Handle(DeleteRecipeCommand request, CancellationToken cancellationToken)
    {
        var slug = string.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            if (RowVersionHeader.TryParse(request.IfMatch, out var expected)
                && !expected.AsSpan().SequenceEqual(recipe.RowVersion))
            {
                throw new ConflictException(
                    ErrorCodes.RecipeConcurrencyConflict,
                    "Dữ liệu đã bị thay đổi bởi người dùng khác. Tải lại công thức rồi thử lại.");
            }

            recipeRepository.Delete(recipe);
            await unitOfWork.SaveChangesAsync(ct);

            slug = recipe.Slug;
        }, cancellationToken);

        // NFR-SEC-006 — audit trail cho lệnh ghi nhạy cảm.
        logger.LogInformation("Recipe {RecipeId} ({Slug}) bị xóa mềm bởi {UserId}", request.RecipeId, slug, currentUser.UserId);

        request.TagsToInvalidate = ["recipes", $"recipe:{slug}"];
    }
}
