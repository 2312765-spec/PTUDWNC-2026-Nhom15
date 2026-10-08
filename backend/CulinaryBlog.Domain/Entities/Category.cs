using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Thực thể Danh mục món ăn (Category Aggregate Root). SRS 7.6.
/// </summary>
public class Category : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public int OrderIndex { get; private set; } = 0;

    private readonly List<Recipe> _recipes = [];
    public IReadOnlyCollection<Recipe> Recipes => _recipes.AsReadOnly();

    private Category()
    {
    }

    private Category(string name, string slug, string? description, string? imageUrl, int orderIndex)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Slug = slug;
        Description = description;
        ImageUrl = imageUrl;
        OrderIndex = orderIndex;
    }

    /// <summary>
    /// FR-CAT-003/FR-CAT-004: Tạo danh mục mới. Tự sinh slug duy nhất (D10) là việc của
    /// handler (ISlugHelper) — hàm này chỉ validate Name/Slug đã có sẵn.
    /// </summary>
    public static Category Create(string name, string slug, string? description = null, string? imageUrl = null, int orderIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tên danh mục không được để trống.", "CATEGORY_NAME_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("Slug danh mục không được để trống.", "CATEGORY_SLUG_REQUIRED");
        }

        return new Category(name, slug, description, imageUrl, orderIndex);
    }

    /// <summary>
    /// FR-CAT-004: Cập nhật tên và mô tả.
    /// Quyết định D10: Slug KHÔNG thay đổi khi cập nhật Name, trừ khi truyền tường minh.
    /// </summary>
    public void Update(string name, string? slug = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tên danh mục không được để trống.", "CATEGORY_NAME_REQUIRED");
        }

        Name = name;
        if (!string.IsNullOrWhiteSpace(slug))
        {
            Slug = slug;
        }
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>FR-CAT-005/D2 — soft delete, không bao giờ xóa cứng.</summary>
    public void Delete() => SoftDelete();
}
