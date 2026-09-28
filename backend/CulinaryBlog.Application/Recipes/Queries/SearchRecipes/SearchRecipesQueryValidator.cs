using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// Validator cho SearchRecipesQuery.
/// Tuân thủ D4: Lỗi validation trả 400 VALIDATION_ERROR (không dùng 422).
/// Ràng buộc: q không rỗng, tối thiểu 2 ký tự.
/// </summary>
public sealed class SearchRecipesQueryValidator : AbstractValidator<SearchRecipesQuery>
{
    public SearchRecipesQueryValidator()
    {
        RuleFor(x => x.Q)
            .NotEmpty().WithMessage("Từ khóa tìm kiếm không được để trống.")
            .MinimumLength(2).WithMessage("Từ khóa tìm kiếm phải có tối thiểu 2 ký tự.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Số trang phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Kích thước trang phải từ 1 đến 50.");
    }
}