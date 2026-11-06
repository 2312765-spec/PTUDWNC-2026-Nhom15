using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Commands;

/// <summary>SRS 7.2.1 — dinh dưỡng trên 1 khẩu phần, mọi trường tùy chọn.</summary>
public sealed record RecipeNutritionInput(
    decimal? Calories,
    decimal? Protein,
    decimal? Carbohydrates,
    decimal? Fat,
    decimal? Fiber,
    decimal? Sodium);

/// <summary>D6 — một bước thực hiện. Không có <c>stepNumber</c>: server sinh theo thứ tự gửi lên.</summary>
public sealed record RecipeStepInput(string Title, string Description, int? TimerMinutes, string? ImageUrl);

/// <summary>D7 — một nguyên liệu. <c>quantity</c>/<c>unit</c> nullable cho "muối vừa đủ".</summary>
public sealed record RecipeIngredientInput(string Name, decimal? Quantity, string? Unit, string? Notes);

/// <summary>Các trường chung của FR-RCP-003 (tạo) và FR-RCP-004 (sửa) — D69.</summary>
public interface IRecipeDetailsInput
{
    string Title { get; }
    string Description { get; }
    Guid CategoryId { get; }
    int PrepTime { get; }
    int CookTime { get; }
    int Servings { get; }

    /// <summary>D14/D69: "Easy" | "Medium" | "Hard" | "Expert" hoặc "1".."4", không phân biệt hoa thường.</summary>
    string Difficulty { get; }

    string? Instructions { get; }
    RecipeNutritionInput? Nutrition { get; }
}

/// <summary>
/// D69 — JSON body nhận <c>difficulty</c> dạng chuỗi ("Hard") HOẶC số (3, đúng kiểu mà
/// <see cref="DTOs.RecipeDetailDto.Difficulty"/> trả về) — cả hai đều đọc thành chuỗi để validator xử lý.
/// </summary>
public sealed class StringOrNumberJsonConverter : System.Text.Json.Serialization.JsonConverter<string>
{
    public override string? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            System.Text.Json.JsonTokenType.Number => reader.TryGetInt64(out var number)
                ? number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.Text.Json.JsonTokenType.Null => null,
            _ => reader.GetString(),
        };

    public override void Write(System.Text.Json.Utf8JsonWriter writer, string value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// <summary>D14/D69 — chuyển chuỗi difficulty client gửi sang enum.</summary>
public static class RecipeDifficultyParser
{
    public static bool TryParse(string? value, out RecipeDifficulty difficulty)
    {
        difficulty = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (int.TryParse(trimmed, out var number))
        {
            if (!Enum.IsDefined(typeof(RecipeDifficulty), number))
            {
                return false;
            }

            difficulty = (RecipeDifficulty)number;
            return true;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out difficulty)
            && Enum.IsDefined(difficulty);
    }

    public static RecipeDifficulty Parse(string value) =>
        TryParse(value, out var difficulty)
            ? difficulty
            : throw new ArgumentException($"Độ khó không hợp lệ: '{value}'.", nameof(value));
}

/// <summary>
/// Ràng buộc chung cho FR-RCP-003/004 (bảng "Ràng buộc validator" + D4, D19, D69).
/// Không đăng ký riêng vào pipeline — chỉ dùng qua <c>Include(...)</c> trong validator của command.
/// </summary>
public sealed class RecipeDetailsValidator : AbstractValidator<IRecipeDetailsInput>
{
    public RecipeDetailsValidator(ICategoryRepository categoryRepository)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .Must(t => t is not null && t.Trim().Length is >= 5 and <= 200)
            .WithMessage("Tiêu đề phải từ 5 đến 200 ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả không được để trống.")
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.");

        RuleFor(x => x.PrepTime).GreaterThan(0).WithMessage("Thời gian chuẩn bị phải lớn hơn 0.");
        RuleFor(x => x.CookTime).GreaterThanOrEqualTo(0).WithMessage("Thời gian nấu không được âm (0 = không cần nấu).");
        RuleFor(x => x.Servings).GreaterThan(0).WithMessage("Khẩu phần phải lớn hơn 0.");

        RuleFor(x => x.Difficulty)
            .Must(d => RecipeDifficultyParser.TryParse(d, out _))
            .WithMessage("Độ khó không hợp lệ. Chỉ chấp nhận: Easy, Medium, Hard, Expert (hoặc 1, 2, 3, 4).");

        // FR-RCP-003 A2 + D4/D69: category không tồn tại (hoặc đã xóa mềm) là lỗi dữ liệu → 400.
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Danh mục không hợp lệ.");
        RuleFor(x => x.CategoryId)
            .MustAsync(async (id, ct) => await categoryRepository.GetByIdAsync(id, ct) is not null)
            .When(x => x.CategoryId != Guid.Empty)
            .WithMessage("Danh mục không hợp lệ.");

        RuleFor(x => x.Nutrition!).SetValidator(new RecipeNutritionInputValidator()).When(x => x.Nutrition is not null);
    }
}

/// <summary>SRS 7.2.1 — numeric(8,2), không âm.</summary>
public sealed class RecipeNutritionInputValidator : AbstractValidator<RecipeNutritionInput>
{
    private const decimal Max = 999_999.99m;

    public RecipeNutritionInputValidator()
    {
        RuleFor(x => x.Calories).InclusiveBetween(0, Max).When(x => x.Calories is not null);
        RuleFor(x => x.Protein).InclusiveBetween(0, Max).When(x => x.Protein is not null);
        RuleFor(x => x.Carbohydrates).InclusiveBetween(0, Max).When(x => x.Carbohydrates is not null);
        RuleFor(x => x.Fat).InclusiveBetween(0, Max).When(x => x.Fat is not null);
        RuleFor(x => x.Fiber).InclusiveBetween(0, Max).When(x => x.Fiber is not null);
        RuleFor(x => x.Sodium).InclusiveBetween(0, Max).When(x => x.Sodium is not null);
    }
}

/// <summary>D6 + bảng "Ràng buộc validator" — RecipeStep.</summary>
public sealed class RecipeStepInputValidator : AbstractValidator<RecipeStepInput>
{
    public RecipeStepInputValidator()
    {
        RecipeStepRules.Apply(this, x => x.Title, x => x.Description, x => x.TimerMinutes, x => x.ImageUrl);
    }
}

/// <summary>D7 — RecipeIngredient.</summary>
public sealed class RecipeIngredientInputValidator : AbstractValidator<RecipeIngredientInput>
{
    public RecipeIngredientInputValidator()
    {
        RecipeIngredientRules.Apply(this, x => x.Name, x => x.Quantity, x => x.Unit, x => x.Notes);
    }
}

/// <summary>D7 — dùng chung cho ingredient trong body tạo recipe (FR-RCP-003) và endpoint ingredient (FR-RCP-009).</summary>
public static class RecipeIngredientRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string>> name,
        System.Linq.Expressions.Expression<Func<T, decimal?>> quantity,
        System.Linq.Expressions.Expression<Func<T, string?>> unit,
        System.Linq.Expressions.Expression<Func<T, string?>> notes)
    {
        validator.RuleFor(name)
            .NotEmpty().WithMessage("Tên nguyên liệu không được để trống.")
            .MaximumLength(200).WithMessage("Tên nguyên liệu không được vượt quá 200 ký tự.");

        // decimal(10,3), nếu có thì phải > 0 (D7).
        var getQuantity = quantity.Compile();
        validator.RuleFor(quantity)
            .GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0 (bỏ trống nếu \"vừa đủ\").")
            .LessThan(10_000_000m).WithMessage("Số lượng quá lớn.")
            .When(x => getQuantity(x) is not null);

        validator.RuleFor(unit).MaximumLength(50).WithMessage("Đơn vị không được vượt quá 50 ký tự.");
        validator.RuleFor(notes).MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}

/// <summary>D6 — dùng chung cho step trong body tạo recipe (FR-RCP-003) và endpoint step (FR-RCP-010).</summary>
public static class RecipeStepRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string>> title,
        System.Linq.Expressions.Expression<Func<T, string>> description,
        System.Linq.Expressions.Expression<Func<T, int?>> timerMinutes,
        System.Linq.Expressions.Expression<Func<T, string?>> imageUrl)
    {
        validator.RuleFor(title)
            .NotEmpty().WithMessage("Tiêu đề bước không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề bước không được vượt quá 200 ký tự.");

        validator.RuleFor(description)
            .NotEmpty().WithMessage("Mô tả bước không được để trống.")
            .MaximumLength(2000).WithMessage("Mô tả bước không được vượt quá 2000 ký tự.");

        validator.RuleFor(timerMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Thời gian của bước không được âm.");

        // Chỉ nhận URL http/https tuyệt đối — chặn "javascript:" khi frontend render ảnh bước.
        var getImageUrl = imageUrl.Compile();
        validator.RuleFor(imageUrl)
            .MaximumLength(500).WithMessage("URL ảnh không được vượt quá 500 ký tự.")
            .Must(BeHttpUrl).WithMessage("URL ảnh phải là địa chỉ http hoặc https hợp lệ.")
            .When(x => !string.IsNullOrEmpty(getImageUrl(x)));
    }

    private static bool BeHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
