using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Ingredients;

/// <summary>
/// FR-RCP-009 — thêm nguyên liệu. D76: khóa dòng recipe trong transaction để hai request song song không cùng tính
/// ra một OrderIndex. D31: recipe đã được track nên PHẢI AddAsync nguyên liệu mới tường minh.
/// </summary>
public sealed class AddRecipeIngredientCommandHandler(
    IRecipeRepository recipeRepository,
    IRepository<RecipeIngredient> ingredientRepository,
    IUnitOfWork unitOfWork,
    RecipeAuthorizationHandler authorization)
    : IRequestHandler<AddRecipeIngredientCommand, RecipeIngredientDto>
{
    public async Task<RecipeIngredientDto> Handle(AddRecipeIngredientCommand request, CancellationToken cancellationToken)
    {
        RecipeIngredientDto? result = null;
        var slug = string.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            var ingredient = recipe.AddIngredient(
                request.Name.Trim(),
                request.Quantity,
                RecipeIngredientText.OptionalTrim(request.Unit),
                RecipeIngredientText.OptionalTrim(request.Notes),
                request.OrderIndex);

            await ingredientRepository.AddAsync(ingredient, ct);
            await unitOfWork.SaveChangesAsync(ct);

            result = RecipeDetailDtoMapper.ToIngredientDto(ingredient);
            slug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{slug}"];
        return result!;
    }
}
