using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Ingredients;

/// <summary>FR-RCP-009 — DELETE /api/v1/recipes/{id}/ingredients/{ingredientId}. Hard delete + đánh lại OrderIndex (D76).</summary>
public sealed record DeleteRecipeIngredientCommand(Guid RecipeId, Guid IngredientId) : IRequest, ICacheInvalidator
{
    /// <summary>D8 — handler gán sau khi biết slug: <c>recipes</c> + <c>recipe:{slug}</c>.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
