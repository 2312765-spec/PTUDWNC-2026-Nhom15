using CulinaryBlog.Domain.Interfaces;
using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Create;

/// <summary>FR-RCP-003 — CONS-008: validation chỉ ở đây (ValidationBehavior), lỗi → 400 VALIDATION_ERROR (D4).</summary>
public sealed class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    /// <summary>Chặn body quá lớn làm một request tạo hàng nghìn dòng con.</summary>
    public const int MaxChildren = 100;

    public CreateRecipeCommandValidator(ICategoryRepository categoryRepository)
    {
        Include(new RecipeDetailsValidator(categoryRepository));

        RuleFor(x => x.Steps)
            .Must(s => s is null || s.Count <= MaxChildren)
            .WithMessage($"Tối đa {MaxChildren} bước thực hiện.");
        RuleForEach(x => x.Steps).SetValidator(new RecipeStepInputValidator());

        RuleFor(x => x.Ingredients)
            .Must(i => i is null || i.Count <= MaxChildren)
            .WithMessage($"Tối đa {MaxChildren} nguyên liệu.");
        RuleForEach(x => x.Ingredients).SetValidator(new RecipeIngredientInputValidator());
    }
}
