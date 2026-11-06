using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Steps;

/// <summary>
/// FR-RCP-010 — PUT /api/v1/recipes/{id}/steps/{stepId}. Thay nội dung bước (title, description bắt buộc;
/// timerMinutes/imageUrl null = xóa). <paramref name="StepNumber"/> có giá trị thì chuyển bước tới vị trí đó và
/// đánh số lại toàn bộ (D6). Trả về mọi bước của recipe theo thứ tự mới (D73).
/// </summary>
public sealed record UpdateRecipeStepCommand(
    Guid RecipeId,
    Guid StepId,
    string Title,
    string Description,
    int? TimerMinutes,
    string? ImageUrl,
    int? StepNumber) : IRequest<IReadOnlyList<RecipeStepDto>>, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
