using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// SRS 7.3 + D6 (field "title" bắt buộc, "timerMinutes" — không dùng "durationMinutes").
/// TODO(S6/S10 — C): auto-renumber StepNumber khi thêm/xóa/sắp xếp lại (D6) chưa hiện thực
/// ở đây — Sprint 0 chỉ tạo shape dữ liệu cho seeding.
/// </summary>
public sealed class RecipeStep : BaseEntity
{
    public Guid RecipeId { get; private set; }
    public int StepNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int? TimerMinutes { get; private set; }
    public string? ImageUrl { get; private set; }

    private RecipeStep()
    {
    }

    public static RecipeStep Create(
        Guid recipeId, int stepNumber, string title, string description,
        int? timerMinutes = null, string? imageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("RECIPE_STEP_TITLE_REQUIRED", "Tên bước không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("RECIPE_STEP_DESCRIPTION_REQUIRED", "Mô tả bước không được để trống.");
        }

        if (timerMinutes is < 0)
        {
            throw new DomainException("RECIPE_STEP_TIMER_INVALID", "Thời gian bước phải >= 0.");
        }

        return new RecipeStep
        {
            RecipeId = recipeId,
            StepNumber = stepNumber,
            Title = title,
            Description = description,
            TimerMinutes = timerMinutes,
            ImageUrl = imageUrl,
        };
    }
}
