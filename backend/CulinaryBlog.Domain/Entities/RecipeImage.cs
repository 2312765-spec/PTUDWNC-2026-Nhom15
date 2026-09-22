using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>SRS 7.5. Logic IsPrimary/OrderIndex (D22/D27) nằm ở <see cref="Recipe"/> — RecipeImage chỉ giữ state.</summary>
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

    /// <summary>Chỉ <see cref="Recipe"/> (aggregate root, cùng assembly) được đổi ảnh nào là primary (D22).</summary>
    internal void SetPrimary(bool value) => IsPrimary = value;

    /// <summary>D27 — field nào null thì giữ nguyên giá trị cũ (PATCH từng phần).</summary>
    internal void UpdateMetadata(string? altText, int? orderIndex)
    {
        if (altText is not null)
        {
            AltText = altText;
        }

        if (orderIndex is not null)
        {
            OrderIndex = orderIndex.Value;
        }
    }
}
