using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>
/// FR-RCP-008/FR-FILE-002/D1 — xóa RecipeImage khỏi DB ngay (hard delete), còn file MinIO xóa
/// bất đồng bộ qua Hangfire (fire-and-forget) — không chờ, không làm fail request nếu MinIO chậm.
/// </summary>
public sealed class DeleteRecipeImageCommandHandler(
    IRecipeRepository recipeRepository,
    IRepository<RecipeImage> imageRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IBackgroundJobService backgroundJobService)
    : IRequestHandler<DeleteRecipeImageCommand>
{
    public async Task Handle(DeleteRecipeImageCommand request, CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetByIdWithImagesAsync(request.RecipeId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

        if (recipe.AuthorId != currentUser.UserId && !currentUser.IsAdmin)
        {
            throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền sửa ảnh của công thức này.");
        }

        var image = recipe.Images.FirstOrDefault(i => i.Id == request.ImageId)
            ?? throw new NotFoundException(ErrorCodes.RecipeImageNotFound, "Không tìm thấy ảnh.");

        // Xóa ảnh khỏi Recipe Aggregate
        recipe.RemoveImage(request.ImageId);

        // Xóa ảnh trực tiếp qua Repository (dùng Delete theo đúng IRepository<T>)
        imageRepository.Delete(image);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{recipe.Slug}"];

        if (!string.IsNullOrEmpty(image.OriginalUrl))
        {
            backgroundJobService.EnqueueDeleteImageFile(image.OriginalUrl);
        }
    }
}