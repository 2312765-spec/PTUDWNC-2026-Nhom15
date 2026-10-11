using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Domain.Interfaces;

public interface IRecipeRepository
{
    /// <summary>FR-RCP-002: chi tiết theo slug kèm category, steps, ingredients, images. Đã xóa → null.</summary>
    Task<Recipe?> GetBySlugDetailedAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-SRCH-001/D49 — tìm kiếm toàn văn trên công thức Published, xếp theo độ liên quan
    /// (ts_rank) giảm dần. Mỗi kết quả kèm điểm Rank. Từ khóa không còn chữ/số nào → rỗng.
    /// </summary>
    Task<(IReadOnlyList<(Recipe Recipe, float Rank)> Items, int TotalCount)> SearchPublishedRecipesAsync(
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Recipe?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default);
    void Update(Recipe recipe);
    void Delete(Recipe recipe);
    Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedByCategoryIdAsync(
        Guid categoryId, RecipeStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<string?> GetAuthorIdAsync(Guid id, CancellationToken ct = default);
    Task<Recipe?> GetByIdWithImagesForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// FR-RCP-001, FR-SRCH-002/003/004: lọc (AND), sắp xếp, phân trang.
    /// Phạm vi trạng thái do Application quyết định:
    /// <paramref name="includeAllStatuses"/> = true → không lọc Status (Admin);
    /// ngược lại chỉ Published, cộng Draft/Archived có AuthorId == <paramref name="nonPublishedOwnerId"/> (nếu có).
    /// </summary>
    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedRecipesAsync(
        Guid? categoryId,
        RecipeDifficulty? difficulty,
        int? maxCookTime,
        int? minServings,
        string? sort,
        bool includeAllStatuses,
        string? nonPublishedOwnerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}