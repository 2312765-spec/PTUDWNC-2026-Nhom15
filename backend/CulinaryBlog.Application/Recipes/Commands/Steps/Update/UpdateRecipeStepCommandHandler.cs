using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Steps;

/// <summary>
/// FR-RCP-010 — sửa / đổi vị trí bước. Thứ tự: 404 recipe → 403 → 404 bước. Vị trí sai → DomainException
/// VALIDATION_ERROR (400). Lưu qua <see cref="IRecipeRepository.SaveStepsAsync"/> (2 pha, D73).
/// </summary>
public sealed class UpdateRecipeStepCommandHandler(
    IRecipeRepository recipeRepository,
    IUnitOfWork unitOfWork,
    RecipeAuthorizationHandler authorization)
    : IRequestHandler<UpdateRecipeStepCommand, IReadOnlyList<RecipeStepDto>>
{
    public async Task<IReadOnlyList<RecipeStepDto>> Handle(UpdateRecipeStepCommand request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RecipeStepDto> result = [];
        var slug = string.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            if (recipe.Steps.All(s => s.Id != request.StepId))
            {
                throw new NotFoundException(ErrorCodes.RecipeStepNotFound, "Không tìm thấy bước thực hiện.");
            }

            recipe.UpdateStep(
                request.StepId,
                request.Title.Trim(),
                request.Description.Trim(),
                request.TimerMinutes,
                string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                request.StepNumber);

            await recipeRepository.SaveStepsAsync(recipe, ct);

            result = RecipeDetailDtoMapper.ToStepDtos(recipe);
            slug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{slug}"];
        return result;
    }
}
