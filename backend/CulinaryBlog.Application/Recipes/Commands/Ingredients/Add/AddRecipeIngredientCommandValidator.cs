using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Ingredients;

/// <summary>FR-RCP-009/D7 — CONS-008, lỗi → 400 VALIDATION_ERROR. Cận trên của orderIndex do domain kiểm tra (D76).</summary>
public sealed class AddRecipeIngredientCommandValidator : AbstractValidator<AddRecipeIngredientCommand>
{
    public AddRecipeIngredientCommandValidator()
    {
        RecipeIngredientRules.Apply(this, x => x.Name, x => x.Quantity, x => x.Unit, x => x.Notes);

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Vị trí nguyên liệu không được âm.")
            .When(x => x.OrderIndex is not null);
    }
}
