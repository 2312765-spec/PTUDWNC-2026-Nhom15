using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Thực thể Công thức nấu ăn (Recipe Entity)
/// Phục vụ việc đếm số món Published trong Category và truy vấn theo danh mục
/// </summary>
public class Recipe
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }
    
    public string AuthorId { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string? AuthorAvatar { get; private set; }

    public RecipeStatus Status { get; private set; } = RecipeStatus.Draft;
    public string Difficulty { get; private set; } = "Easy";
    public int PrepTimeMinutes { get; private set; }
    public int CookTimeMinutes { get; private set; }
    public int Servings { get; private set; }
    public string ThumbnailUrl { get; private set; } = string.Empty;

    public int ViewCount { get; private set; }
    public int LikeCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Recipe() { }

    public static Recipe Create(
        string title,
        string slug,
        string description,
        Guid categoryId,
        string authorId,
        string authorName,
        string? authorAvatar,
        RecipeStatus status,
        string difficulty,
        int prepTimeMinutes,
        int cookTimeMinutes,
        int servings,
        string thumbnailUrl)
    {
        return new Recipe
        {
            Id = Guid.NewGuid(),
            Title = title,
            Slug = slug,
            Description = description,
            CategoryId = categoryId,
            AuthorId = authorId,
            AuthorName = authorName,
            AuthorAvatar = authorAvatar,
            Status = status,
            Difficulty = difficulty,
            PrepTimeMinutes = prepTimeMinutes,
            CookTimeMinutes = cookTimeMinutes,
            Servings = servings,
            ThumbnailUrl = thumbnailUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
