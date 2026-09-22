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

        // D22/D27: DomainException (vd RECIPE_PRIMARY_IMAGE_REQUIRED) nằm trong Recipe.UpdateImage.
        recipe.UpdateImage(request.ImageId, request.AltText, request.IsPrimary, request.OrderIndex);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{recipe.Slug}"];
    }
}
