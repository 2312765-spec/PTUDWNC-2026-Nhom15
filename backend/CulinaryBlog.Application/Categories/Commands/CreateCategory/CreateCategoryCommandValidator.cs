using System.Text.RegularExpressions;
using FluentValidation;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    private static readonly Regex _htmlRegex = new(@"<[^>]*>", RegexOptions.Compiled);

    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .Length(2, 50).WithMessage("Tên danh mục phải từ 2 đến 50 ký tự.")
            .Must(name => !_htmlRegex.IsMatch(name ?? string.Empty))
            .WithMessage("Tên danh mục không được chứa mã HTML.");

        RuleFor(x => x.Description)
            .Must(desc => string.IsNullOrEmpty(desc) || !_htmlRegex.IsMatch(desc))
            .WithMessage("Mô tả không được chứa mã HTML.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}