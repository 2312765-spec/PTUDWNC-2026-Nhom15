using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

/// <summary>
/// Repository interface cho Category entity.
/// Nằm trong Tầng Domain (Domain Layer).
/// Tuân thủ Clean Architecture (CONS-001, NFR-MAINT-004):
/// Tầng Domain KHÔNG reference Tầng Application hay Infrastructure.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// FR-CAT-001: Lấy tất cả danh mục kèm các công thức đã xuất bản (Published).
    /// Sắp xếp theo Name tăng dần.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Danh sách thực thể Category kèm Recipes</returns>
    Task<IReadOnlyList<Category>> GetAllWithRecipesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-002: Lấy danh mục theo Slug.
    /// </summary>
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra tồn tại theo tên danh mục.
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra tồn tại theo Slug.
    /// </summary>
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-003: Thêm danh mục mới.
    /// </summary>
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-004: Lấy danh mục theo ID.
    /// </summary>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đếm số công thức đã xuất bản trong danh mục.
    /// </summary>
    Task<int> GetPublishedRecipeCountAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-004: Cập nhật thông tin danh mục.
    /// </summary>
    void Update(Category category);

    /// <summary>
    /// FR-CAT-005: Xóa mềm danh mục (Soft Delete).
    /// </summary>
    void Delete(Category category);
}
