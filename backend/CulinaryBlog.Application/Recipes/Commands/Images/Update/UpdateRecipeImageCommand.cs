using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>FR-RCP-008/D22 — PATCH /recipes/{id}/images/{imageId}, một endpoint chung cho mọi metadata.</summary>
public sealed record UpdateRecipeImageCommand(
    Guid RecipeId,
    Guid ImageId,
    string? AltText,
    bool? IsPrimary,
    int? OrderIndex) : IRequest, ICacheInvalidator
{
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
