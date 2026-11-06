using CulinaryBlog.Application.Recipes.Commands.Update;
using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Delete;

/// <summary>FR-RCP-007/D78 — If-Match tùy chọn, nhưng đã gửi thì phải đúng định dạng (D71). Lỗi → 400.</summary>
public sealed class DeleteRecipeCommandValidator : AbstractValidator<DeleteRecipeCommand>
{
    public DeleteRecipeCommandValidator()
    {
        RuleFor(x => x.IfMatch)
            .Must(v => RowVersionHeader.TryParse(v, out _))
            .WithMessage("Header If-Match sai định dạng — gửi lại rowVersion nhận được khi đọc công thức, hoặc bỏ header.")
            .OverridePropertyName("RowVersion")
            .When(x => !string.IsNullOrWhiteSpace(x.IfMatch));
    }
}
