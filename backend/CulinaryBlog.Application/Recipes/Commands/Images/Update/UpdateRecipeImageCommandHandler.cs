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

            _ = recipe.Images.FirstOrDefault(i => i.Id == request.ImageId)
                ?? throw new NotFoundException(ErrorCodes.RecipeImageNotFound, "Không tìm thấy ảnh.");

            // D31: demote sớm + save riêng trước khi set ảnh mới thành primary, tránh vi phạm unique
            // index "1 primary/recipe" khi 2 dòng cùng IsPrimary=true tồn tại giữa transaction.
            if (request.IsPrimary == true)
            {
                recipe.DemoteOtherPrimaryImages(request.ImageId);
                await unitOfWork.SaveChangesAsync(ct);
            }

            // D22/D23: cả AltText/IsPrimary/OrderIndex được áp dụng trong CÙNG một lần gọi domain —
            // PATCH chỉ gửi orderIndex (kéo-thả sắp xếp) vẫn phải lưu được, không rơi vào nhánh nào bị bỏ sót.
            try
            {
                recipe.UpdateImage(request.ImageId, request.AltText, request.IsPrimary, request.OrderIndex);
            }
            catch (DomainException ex)
            {
                throw new BadRequestException(
                    string.IsNullOrEmpty(ex.ErrorCode) ? ErrorCodes.RecipePrimaryImageRequired : ex.ErrorCode,
                    ex.Message);
            }

            await unitOfWork.SaveChangesAsync(ct);

            recipeSlug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{recipeSlug}"];
    }
}