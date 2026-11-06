using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Steps;

/// <summary>
/// FR-RCP-010 — thêm bước. D73: khóa dòng recipe trong transaction để hai request song song không cùng tính
/// ra Max + 1. D31: recipe đã được track nên PHẢI AddAsync bước mới tường minh.
/// </summary>
public sealed class AddRecipeStepCommandHandler(
    IRecipeRepository recipeRepository,
    IRepository<RecipeStep> stepRepository,
    IUnitOfWork unitOfWork,
    RecipeAuthorizationHandler authorization)
    : IRequestHandler<AddRecipeStepCommand, RecipeStepDto>
{
    public async Task<RecipeStepDto> Handle(AddRecipeStepCommand request, CancellationToken cancellationToken)
    {
        RecipeStepDto? result = null;
        var slug = string.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var recipe = await recipeRepository.GetByIdWithDetailsForUpdateAsync(request.RecipeId, ct)
                ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

            authorization.EnsureCanModify(recipe);

            var step = recipe.AppendStep(
                request.Title.Trim(),
                request.Description.Trim(),
                request.TimerMinutes,
                string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim());

            await stepRepository.AddAsync(step, ct);
            await unitOfWork.SaveChangesAsync(ct);

            result = RecipeDetailDtoMapper.ToStepDto(step);
            slug = recipe.Slug;
        }, cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{slug}"];
        return result!;
    }
}
