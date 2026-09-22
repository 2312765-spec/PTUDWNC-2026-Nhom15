namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>D27 — response 201 theo SRS mục 8.4: { imageId, originalUrl, altText, isPrimary }.</summary>
public sealed record UploadRecipeImageResult(Guid ImageId, string OriginalUrl, string? AltText, bool IsPrimary);
