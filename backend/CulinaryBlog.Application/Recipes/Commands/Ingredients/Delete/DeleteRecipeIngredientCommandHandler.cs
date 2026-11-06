using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Ingredients;

/// <summary>
/// FR-RCP-009 — xóa nguyên liệu. D76: hard delete (giống bước — D73, ảnh — D43); D1 chỉ áp cho Recipe.
/// Recipe Published không được mất nguyên liệu cuối (D3).
/// </summary>
public sealed class DeleteRecipeIngredientCommandHandler(
    IRecipeRepository recipeRepository,
    IRepository<RecipeIngredient> ingredientRepository,
    IUnitOfWork unitOfWork,
    RecipeAuthorizationHandler authorization)
    : IRequestHandler<DeleteRecipeIngredientCommand>
{
    public async Task Handle(DeleteRecipeIngredientCommand request, CancellationToken cancellationToken)
    {
        var slug = string.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            var ingredient = recipe.Ingredients.FirstOrDefault(i => i.Id == request.IngredientId)
                ?? throw new NotFoundException(ErrorCodes.RecipeIngredientNotFound, "Không tìm thấy nguyên liệu.");

            recipe.RemoveIngredient(ingredient.Id);
            ingredientRepository.Delete(ingredient);

            await unitOfWork.SaveChangesAsync(ct);

            slug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{slug}"];
    }
}
