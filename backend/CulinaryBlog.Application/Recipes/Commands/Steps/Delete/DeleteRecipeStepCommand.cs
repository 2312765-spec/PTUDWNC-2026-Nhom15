using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Steps;

/// <summary>FR-RCP-010 — DELETE /api/v1/recipes/{id}/steps/{stepId}. Hard delete + đánh số lại (D6, D73).</summary>
public sealed record DeleteRecipeStepCommand(Guid RecipeId, Guid StepId) : IRequest, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
