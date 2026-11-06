using CulinaryBlog.Domain.Interfaces;
using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Update;

/// <summary>FR-RCP-004 — dùng chung ràng buộc với FR-RCP-003 (D69) + If-Match bắt buộc (D71). Lỗi → 400.</summary>
public sealed class UpdateRecipeCommandValidator : AbstractValidator<UpdateRecipeCommand>
{
    public UpdateRecipeCommandValidator(ICategoryRepository categoryRepository)
    {
        Include(new RecipeDetailsValidator(categoryRepository));

        RuleFor(x => x.IfMatch)
            .Must(v => RowVersionHeader.TryParse(v, out _))
            .WithMessage("Thiếu hoặc sai header If-Match — gửi lại rowVersion nhận được khi đọc công thức.")
            .OverridePropertyName("RowVersion");
    }
}
