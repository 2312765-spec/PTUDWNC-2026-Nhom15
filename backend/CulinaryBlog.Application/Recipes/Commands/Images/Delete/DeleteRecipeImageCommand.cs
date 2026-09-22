using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>FR-RCP-008 — DELETE /recipes/{id}/images/{imageId}. Hard delete (SRS bước 13) — khác D1 (Recipe/Category soft delete).</summary>
public sealed record DeleteRecipeImageCommand(Guid RecipeId, Guid ImageId) : IRequest, ICacheInvalidator
{
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
