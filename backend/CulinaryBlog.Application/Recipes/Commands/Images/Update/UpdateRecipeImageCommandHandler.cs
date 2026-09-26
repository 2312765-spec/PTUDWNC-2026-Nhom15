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
        string recipeSlug = string.Empty;

        // D22/D27: đổi primary cần 2 lần SaveChanges (hạ ảnh cũ → nâng ảnh mới) để không vi phạm unique
        // index; gói cả hai trong 1 transaction để lần lưu thứ hai lỗi thì ảnh cũ không bị mất primary.
        // Khoá dòng Recipe (như upload): hai PATCH isPrimary=true đồng thời vào hai ảnh khác nhau nếu
        // không xếp hàng sẽ cùng thấy một ảnh primary cũ và cùng nâng ảnh của mình → 500.
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithImagesForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            if (recipe.AuthorId != currentUser.UserId && !currentUser.IsAdmin)
            {
                throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền sửa ảnh của công thức này.");
            }

            if (!recipe.Images.Any(i => i.Id == request.ImageId))
            {
                throw new NotFoundException(ErrorCodes.RecipeImageNotFound, "Không tìm thấy ảnh.");
            }

            if (request.IsPrimary == true)
            {
                recipe.DemoteOtherPrimaryImages(request.ImageId);
                await unitOfWork.SaveChangesAsync(ct);
            }

            // D22/D27: DomainException (vd RECIPE_PRIMARY_IMAGE_REQUIRED) nằm trong Recipe.UpdateImage.
            recipe.UpdateImage(request.ImageId, request.AltText, request.IsPrimary, request.OrderIndex);
            await unitOfWork.SaveChangesAsync(ct);

            recipeSlug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{recipeSlug}"];
    }
}
