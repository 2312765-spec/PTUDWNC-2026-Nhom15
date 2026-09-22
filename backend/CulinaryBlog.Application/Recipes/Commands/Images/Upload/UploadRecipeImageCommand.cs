using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>FR-RCP-008/D27 — POST /recipes/{id}/images. Client KHÔNG gửi isPrimary (D22 — server tự quyết).</summary>
public sealed record UploadRecipeImageCommand(
    Guid RecipeId,
    Stream Content,
    long Length,
    string ContentType,
    string FileName,
    string? AltText) : IRequest<UploadRecipeImageResult>, ICacheInvalidator
{
    /// <summary>Chỉ biết được sau khi handler load Recipe (cần Slug) — xem D8, CacheKeys ở Infrastructure.</summary>
    public IReadOnlyList<string> TagsToInvalidate { get; internal set; } = [];
}
