using System.Text.RegularExpressions;
using FluentValidation;

namespace CulinaryBlog.Application.Categories.Commands.UpdateCategory;

/// <summary>FR-CAT-004 — cùng luật HTML với CreateCategoryCommandValidator (FR-CAT-003) để Update không lách được.</summary>
public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    private static readonly Regex _htmlRegex = new(@"<[^>]*>", RegexOptions.Compiled);

    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID danh mục không được để trống.");

        // `?? string.Empty`: body thiếu name thì Name = null, Must vẫn chạy sau NotEmpty (cascade Continue).
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MinimumLength(2).WithMessage("Tên danh mục phải có ít nhất 2 ký tự.")
            .MaximumLength(50).WithMessage("Tên danh mục không được vượt quá 50 ký tự.")
            .Must(name => !_htmlRegex.IsMatch(name ?? string.Empty))
            .WithMessage("Tên danh mục không được chứa mã HTML.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.")
            .Must(desc => !_htmlRegex.IsMatch(desc ?? string.Empty))
            .WithMessage("Mô tả không được chứa mã HTML.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}