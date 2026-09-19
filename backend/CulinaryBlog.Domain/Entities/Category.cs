namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Thực thể Danh mục món ăn (Category Entity - SRS Module FR-CAT)
/// Được quản lý bởi Admin; Author và Guest chỉ có quyền đọc.
/// </summary>
public class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public int OrderIndex { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Navigation property
    public ICollection<Recipe> Recipes { get; private set; } = new List<Recipe>();

    // Private constructor cho EF Core
    private Category() { }

    /// <summary>
    /// Factory Method: Category.Create(name, slug, description) theo SRS FR-CAT-003 Luồng chính bước 7
    /// </summary>
    public static Category Create(
        string name, 
        string slug, 
        string? description = null, 
        string? imageUrl = null, 
        int orderIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên danh mục không được để trống.", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug không được để trống.", nameof(slug));

        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            ImageUrl = imageUrl?.Trim(),
            OrderIndex = orderIndex,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string name, string? description, string? imageUrl, int orderIndex)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên danh mục không được để trống.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        ImageUrl = imageUrl?.Trim();
        OrderIndex = orderIndex;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
