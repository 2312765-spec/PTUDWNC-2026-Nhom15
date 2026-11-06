using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Steps;

/// <summary>
/// FR-RCP-010 — xóa bước. D73: hard delete (giống ảnh — D43), vì bước đã xóa mềm vẫn giữ chỗ trong unique
/// index (RecipeId, StepNumber) và chặn việc đánh số lại. Recipe Published không được mất bước cuối (D3).
/// </summary>
public sealed class DeleteRecipeStepCommandHandler(
    IRecipeRepository recipeRepository,
    IRepository<RecipeStep> stepRepository,
    IUnitOfWork unitOfWork,
    RecipeAuthorizationHandler authorization)
    : IRequestHandler<DeleteRecipeStepCommand>
{
    public async Task Handle(DeleteRecipeStepCommand request, CancellationToken cancellationToken)
    {
        var slug = string.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            var step = recipe.Steps.FirstOrDefault(s => s.Id == request.StepId)
                ?? throw new NotFoundException(ErrorCodes.RecipeStepNotFound, "Không tìm thấy bước thực hiện.");

            recipe.RemoveStep(step.Id);
            stepRepository.Delete(step);

            await recipeRepository.SaveStepsAsync(recipe, ct);

            slug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{slug}"];
    }
}
