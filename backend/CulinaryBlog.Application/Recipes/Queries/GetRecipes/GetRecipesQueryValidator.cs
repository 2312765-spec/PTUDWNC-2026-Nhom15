using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipes;

public sealed class GetRecipesQueryValidator : AbstractValidator<GetRecipesQuery>
{
    private static readonly string[] _allowedSortFields =
    [
        "createdat", "-createdat",
        "title", "-title",
        "cooktime", "-cooktime"
    ];

    private static readonly string[] _allowedDifficulties =
    [
        "easy", "medium", "hard", "1", "2", "3"
    ];

    public GetRecipesQueryValidator()
    {
        // FR-SRCH-004: page < 1 -> báo lỗi 400
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Số trang phải lớn hơn hoặc bằng 1.");

        // FR-SRCH-002: Kiểm tra các bộ lọc
        RuleFor(x => x.MaxCookTime)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxCookTime.HasValue)
            .WithMessage("Thời gian nấu tối đa phải lớn hơn hoặc bằng 0.");

        RuleFor(x => x.MinServings)
            .GreaterThan(0)
            .When(x => x.MinServings.HasValue)
            .WithMessage("Khẩu phần tối thiểu phải lớn hơn 0.");

        RuleFor(x => x.Difficulty)
            .Must(d => string.IsNullOrEmpty(d) || _allowedDifficulties.Contains(d.Trim().ToLowerInvariant()))
            .WithMessage("Độ khó không hợp lệ. Chỉ chấp nhận: Easy, Medium, Hard (hoặc 1, 2, 3).");

        // FR-SRCH-003: Sắp xếp ngoài danh sách -> 400
        RuleFor(x => x.Sort)
            .Must(s => string.IsNullOrEmpty(s) || _allowedSortFields.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Trường sắp xếp không hợp lệ. Chỉ chấp nhận: createdAt, -createdAt, title, -title, cookTime, -cookTime.");
    }
}