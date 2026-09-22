using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>FR-RCP-008/D22/D27 — PATCH rỗng (không field nào) là lỗi validation.</summary>
public sealed class UpdateRecipeImageCommandValidator : AbstractValidator<UpdateRecipeImageCommand>
{
    public UpdateRecipeImageCommandValidator()
    {
        RuleFor(x => x.AltText).MaximumLength(200);
        RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0).When(x => x.OrderIndex is not null);

        RuleFor(x => x)
            .Must(x => x.AltText is not null || x.IsPrimary is not null || x.OrderIndex is not null)
            .WithMessage("Phải có ít nhất một trường để cập nhật (altText, isPrimary hoặc orderIndex).");
    }
}
