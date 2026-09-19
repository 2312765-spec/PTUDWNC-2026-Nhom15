using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

/// <summary>
/// Repository Interface cho Module Quản lý Danh mục (FR-CAT)
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// FR-CAT-001: Lấy danh sách tất cả danh mục kèm số lượng công thức Published
    /// </summary>
    Task<IReadOnlyList<(Category Category, int PublishedRecipeCount)>> GetAllWithRecipeCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-002: Lấy thông tin danh mục theo SEO friendly Slug
    /// </summary>
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-003: Kiểm tra tên danh mục đã tồn tại chưa (chống trùng lặp 409 Conflict)
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-003: Kiểm tra slug đã tồn tại chưa (phục vụ sinh suffix "-2", "-3")
    /// </summary>
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-003: Thêm mới Category entity vào DbContext
    /// </summary>
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-004, FR-CAT-005: Tìm kiếm danh mục theo Primary Key ID
    /// </summary>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-004: Kiểm tra tên danh mục trùng lặp nhưng loại trừ ID hiện tại
    /// </summary>
    Task<bool> ExistsByNameExcludingIdAsync(string name, Guid excludeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-005: Đếm tổng số công thức (cả Published lẫn Draft/Archived) thuộc danh mục để kiểm tra ràng buộc trước khi xóa
    /// </summary>
    Task<int> CountRecipesAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-005: Xóa Category entity khỏi DbContext
    /// </summary>
    Task DeleteAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-002: Lấy danh sách công thức thuộc danh mục với phân quyền:
    /// Guest: chỉ lấy Published.
    /// Author: lấy Published + Draft của chính mình.
    /// Admin: lấy tất cả.
    /// Phân trang OFFSET-based: SKIP (page-1)*pageSize TAKE pageSize.
    /// </summary>
    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetRecipesByCategoryAsync(
        Guid categoryId, 
        string? currentUserId, 
        bool isAdmin, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default);
}
