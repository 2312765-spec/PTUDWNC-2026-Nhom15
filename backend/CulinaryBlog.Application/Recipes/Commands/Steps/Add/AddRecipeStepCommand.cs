using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Steps;

/// <summary>
/// FR-RCP-010 — POST /api/v1/recipes/{id}/steps. Body KHÔNG có stepNumber: server đặt Max + 1 (D6).
/// </summary>
public sealed record AddRecipeStepCommand(
    Guid RecipeId,
    string Title,
    string Description,
    int? TimerMinutes,
    string? ImageUrl) : IRequest<RecipeStepDto>, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
