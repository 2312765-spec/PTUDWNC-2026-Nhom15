using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Steps;

/// <summary>
/// FR-RCP-010/D6 — CONS-008. Cận trên của stepNumber (≤ số bước) phụ thuộc dữ liệu, do domain kiểm tra (D73).
/// </summary>
public sealed class UpdateRecipeStepCommandValidator : AbstractValidator<UpdateRecipeStepCommand>
{
    public UpdateRecipeStepCommandValidator()
    {
        RecipeStepRules.Apply(this, x => x.Title, x => x.Description, x => x.TimerMinutes, x => x.ImageUrl);

        RuleFor(x => x.StepNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Vị trí bước phải lớn hơn hoặc bằng 1.")
            .When(x => x.StepNumber is not null);
    }
}
