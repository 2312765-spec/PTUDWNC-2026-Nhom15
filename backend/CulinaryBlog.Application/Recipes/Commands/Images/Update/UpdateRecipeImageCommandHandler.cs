using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

public sealed class UpdateRecipeImageCommandHandler(
    IRecipeRepository recipeRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateRecipeImageCommand>
{
    public async Task Handle(UpdateRecipeImageCommand request, CancellationToken cancellationToken)
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

        // D22: Nếu đặt làm ảnh chính, hạ ảnh chính cũ về false và LƯU TRƯỚC VÀO DB
        // Tránh lỗi 500 do vi phạm Unique Index ("ix_recipe_images_recipe_id_is_primary") trên PostgreSQL
        if (request.IsPrimary == true)
        {
            recipe.DemoteCurrentPrimaryImage(request.ImageId);
            await unitOfWork.SaveChangesAsync(cancellationToken); // <-- Lưu đợt 1
        }

        // D22/D27: Cập nhật ảnh mục tiêu thành Primary và cập nhật Metadata
        recipe.UpdateImage(request.ImageId, request.AltText, request.IsPrimary, request.OrderIndex);

        await unitOfWork.SaveChangesAsync(cancellationToken); // <-- Lưu đợt 2

        request.TagsToInvalidate = ["recipes", $"recipe:{recipe.Slug}"];
    }
}