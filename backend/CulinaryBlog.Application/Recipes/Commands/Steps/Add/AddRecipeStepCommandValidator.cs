using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Steps;

/// <summary>FR-RCP-010/D6 — CONS-008, lỗi → 400 VALIDATION_ERROR.</summary>
public sealed class AddRecipeStepCommandValidator : AbstractValidator<AddRecipeStepCommand>
{
    public AddRecipeStepCommandValidator()
    {
        RecipeStepRules.Apply(this, x => x.Title, x => x.Description, x => x.TimerMinutes, x => x.ImageUrl);
    }
}
