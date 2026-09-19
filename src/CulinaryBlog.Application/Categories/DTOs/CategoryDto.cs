namespace CulinaryBlog.Application.Categories.DTOs;

/// <summary>
/// Category DTO theo đặc tả SRS FR-CAT-001 & FR-CAT-003
/// </summary>
public record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    int RecipeCount,
    int OrderIndex
);

/// <summary>
/// Recipe Summary DTO dùng trong phân trang danh mục (FR-CAT-002)
/// </summary>
public record RecipeSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string Description,
    string AuthorId,
    string AuthorName,
    string? AuthorAvatar,
    string Status,
    string Difficulty,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    int Servings,
    string ThumbnailUrl,
    int LikeCount,
    DateTimeOffset CreatedAt
);

/// <summary>
/// PagedResult DTO theo chuẩn OFFSET-based pagination
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}

/// <summary>
/// Kết quả chi tiết danh mục kèm danh sách công thức phân trang (FR-CAT-002)
/// </summary>
public record CategoryDetailDto(
    CategoryDto Category,
    PagedResult<RecipeSummaryDto> Recipes
);
