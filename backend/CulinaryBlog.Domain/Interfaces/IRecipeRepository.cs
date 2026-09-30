using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Domain.Interfaces;

public interface IRecipeRepository
{
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

    // FR-SRCH-001: Tìm kiếm toàn văn
    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> SearchPublishedRecipesAsync(
        string? searchTerm, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default);

    // FR-RCP-001, FR-SRCH-002, FR-SRCH-003, FR-SRCH-004: Lọc, sắp xếp, phân trang
    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedRecipesAsync(
        Guid? categoryId,
        string? difficulty,
        int? maxCookTime,
        int? minServings,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}