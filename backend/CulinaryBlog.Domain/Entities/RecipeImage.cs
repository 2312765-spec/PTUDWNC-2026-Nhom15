using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// SRS 7.5. TODO(S8 — D): logic IsPrimary (ảnh đầu tự động, xóa → ảnh kế lên) chưa
/// hiện thực ở đây — Sprint 0 chỉ tạo shape dữ liệu cho seeding.
/// </summary>
public sealed class RecipeImage : BaseEntity
{
    public Guid RecipeId { get; private set; }
    public string OriginalUrl { get; private set; } = string.Empty;
    public string? MediumUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? AltText { get; private set; }
    public bool IsPrimary { get; private set; }
    public int OrderIndex { get; private set; }

    private RecipeImage()
    {
    }

    public static RecipeImage Create(
        Guid recipeId, string originalUrl, bool isPrimary = false, int orderIndex = 0,
        string? mediumUrl = null, string? thumbnailUrl = null, string? altText = null)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            throw new DomainException("RECIPE_IMAGE_URL_REQUIRED", "URL ảnh gốc không được để trống.");
        }

        return new RecipeImage
        {
            RecipeId = recipeId,
            OriginalUrl = originalUrl,
            MediumUrl = mediumUrl,
            ThumbnailUrl = thumbnailUrl,
            AltText = altText,
            IsPrimary = isPrimary,
            OrderIndex = orderIndex,
        };
    }
}
