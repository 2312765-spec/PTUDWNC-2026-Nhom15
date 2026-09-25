using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>SRS 7.6.</summary>
public sealed class Category : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public int OrderIndex { get; private set; }

    private readonly List<Recipe> _recipes = [];
    public IReadOnlyCollection<Recipe> Recipes => _recipes.AsReadOnly();

    private Category()
    {
    }

    /// <summary>
    /// FR-CAT-004: Cập nhật tên và mô tả.
    /// Quyết định bắt buộc: Slug KHÔNG thay đổi khi cập nhật Name. UpdatedAt do AuditInterceptor set.
    /// </summary>
    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("CATEGORY_NAME_REQUIRED", "Tên danh mục không được để trống.");
        }

        Name = name;
        Description = description;
    }

    /// <summary>
    /// Dùng cho seeding Sprint 0. Validate + auto-suffix slug (D10) là việc của
    /// FR-CAT-003 (Sprint 1 — B), chưa hiện thực ở đây.
    /// </summary>
    public static Category Create(string name, string slug, string? description, string? imageUrl, int orderIndex)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("CATEGORY_NAME_REQUIRED", "Tên danh mục không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("CATEGORY_SLUG_REQUIRED", "Slug danh mục không được để trống.");
        }

        return new Category
        {
            Name = name,
            Slug = slug,
            Description = description,
            ImageUrl = imageUrl,
            OrderIndex = orderIndex,
        };
    }
}
