using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Ingredients;

/// <summary>
/// FR-RCP-009 — sửa / đổi vị trí nguyên liệu. Thứ tự: 404 recipe → 403 → 404 nguyên liệu. Vị trí sai →
/// DomainException VALIDATION_ERROR (400). Không có unique index trên OrderIndex nên lưu một lần (D76).
/// </summary>
public sealed class UpdateRecipeIngredientCommandHandler(
    IRecipeRepository recipeRepository,
    IUnitOfWork unitOfWork,
    RecipeAuthorizationHandler authorization)
    : IRequestHandler<UpdateRecipeIngredientCommand, IReadOnlyList<RecipeIngredientDto>>
{
    public async Task<IReadOnlyList<RecipeIngredientDto>> Handle(UpdateRecipeIngredientCommand request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RecipeIngredientDto> result = [];
        var slug = string.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            if (recipe.Ingredients.All(i => i.Id != request.IngredientId))
            {
                throw new NotFoundException(ErrorCodes.RecipeIngredientNotFound, "Không tìm thấy nguyên liệu.");
            }

            recipe.UpdateIngredient(
                request.IngredientId,
                request.Name.Trim(),
                request.Quantity,
                RecipeIngredientText.OptionalTrim(request.Unit),
                RecipeIngredientText.OptionalTrim(request.Notes),
                request.OrderIndex);

            await unitOfWork.SaveChangesAsync(ct);

            result = RecipeDetailDtoMapper.ToIngredientDtos(recipe);
            slug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{slug}"];
        return result;
    }
}
