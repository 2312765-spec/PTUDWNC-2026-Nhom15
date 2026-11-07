using CulinaryBlog.Application.Recipes.Commands.Update;
using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands.Archive;

/// <summary>FR-RCP-006/D79 — If-Match tùy chọn, nhưng đã gửi thì phải đúng định dạng (D71). Lỗi → 400.</summary>
public sealed class ArchiveRecipeCommandValidator : AbstractValidator<ArchiveRecipeCommand>
{
    public ArchiveRecipeCommandValidator()
    {
        RuleFor(x => x.IfMatch)
            .Must(v => RowVersionHeader.TryParse(v, out _))
            .WithMessage("Header If-Match sai định dạng — gửi lại rowVersion nhận được khi đọc công thức, hoặc bỏ header.")
            .OverridePropertyName("RowVersion")
            .When(x => !string.IsNullOrWhiteSpace(x.IfMatch));
    }
}
