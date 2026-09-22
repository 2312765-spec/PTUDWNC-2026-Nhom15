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

        if (!recipe.Images.Any(i => i.Id == request.ImageId))
        {
            throw new NotFoundException(ErrorCodes.RecipeImageNotFound, "Không tìm thấy ảnh.");
        }

        var image = recipe.RemoveImage(request.ImageId);
        imageRepository.Remove(image);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{recipe.Slug}"];

        backgroundJobService.EnqueueDeleteImageFile(image.OriginalUrl);
    }
}
