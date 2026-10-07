using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>FR-JOB-002 — CONS-008: validate qua pipeline dù command chỉ do job nội bộ gửi.</summary>
public sealed class GenerateRecipeImageVariantsCommandValidator : AbstractValidator<GenerateRecipeImageVariantsCommand>
{
    public GenerateRecipeImageVariantsCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.ImageId).NotEmpty();
    }
}
