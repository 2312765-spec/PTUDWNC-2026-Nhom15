using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Exceptions;
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

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithImagesForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            if (recipe.AuthorId != currentUser.UserId && !currentUser.IsAdmin)
            {
                throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền sửa ảnh của công thức này.");
            }

            var targetImage = recipe.Images.FirstOrDefault(i => i.Id == request.ImageId)
                ?? throw new NotFoundException(ErrorCodes.RecipeImageNotFound, "Không tìm thấy ảnh.");

            // D22/D27: Nếu cố hạ ảnh primary về false nhưng không còn ảnh nào khác làm primary (hoặc là ảnh duy nhất)
            // thì phải trả về 400 RECIPE_PRIMARY_IMAGE_REQUIRED
            if (request.IsPrimary == false && targetImage.IsPrimary)
            {
                var otherPrimaryExists = recipe.Images.Any(i => i.Id != request.ImageId && i.IsPrimary);
                if (!otherPrimaryExists)
                {
                    throw new BadRequestException(ErrorCodes.RecipePrimaryImageRequired, "Công thức phải có ít nhất một ảnh chính.");
                }
            }

            if (request.IsPrimary == true)
            {
                recipe.DemoteOtherPrimaryImages(request.ImageId);
                await unitOfWork.SaveChangesAsync(ct);
            }

            try
            {
                if (request.IsPrimary.HasValue)
                {
                    recipe.UpdateImage(request.ImageId, request.IsPrimary.Value, request.OrderIndex);
                }
                
                if (!string.IsNullOrEmpty(request.AltText))
                {
                    recipe.UpdateImage(request.ImageId, request.AltText, request.OrderIndex);
                }
            }
            catch (DomainException ex)
            {
                throw new BadRequestException(
                    string.IsNullOrEmpty(ex.ErrorCode) ? ErrorCodes.RecipePrimaryImageRequired : ex.ErrorCode, 
                    ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new BadRequestException(ErrorCodes.RecipePrimaryImageRequired, ex.Message);
            }

            await unitOfWork.SaveChangesAsync(ct);

            recipeSlug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{recipeSlug}"];
    }
}