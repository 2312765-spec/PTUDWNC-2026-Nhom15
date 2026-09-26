using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Thực thể Danh mục món ăn (Category Aggregate Root).
/// </summary>
public class Category : BaseEntity, IAggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

    public Category()
    {
    }

    public Category(string name, string slug, string? description = null)
    {
        Name = name;
        Slug = slug;
        Description = description;
    }

    // DUY NHẤT 1 hàm Update hỗ trợ cả slug có hoặc không có giá trị
    public void Update(string name, string? slug = null, string? description = null)
    {
        Name = name;
        if (!string.IsNullOrWhiteSpace(slug))
        {
            Slug = slug;
        }
        Description = description;
    }

    public static Category Create(string name, string slug, string? description = null)
    {
        return new Category(name, slug, description);
    }

    public static Category Create(string name, string slug, string? description, object? arg4, object? arg5)
    {
        return new Category(name, slug, description);
    }
}