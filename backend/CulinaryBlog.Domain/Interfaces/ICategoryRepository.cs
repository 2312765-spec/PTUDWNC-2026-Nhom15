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
    /// <summary>Lấy tất cả danh mục chưa xóa mềm.</summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-001 / NFR-PERF-004: mọi danh mục chưa xóa kèm số công thức Published chưa xóa, đếm trong
    /// database (projection) — không nạp recipe. Sắp xếp theo Name tăng dần.
    /// </summary>
    Task<IReadOnlyList<CategorySummary>> GetAllWithPublishedRecipeCountAsync(CancellationToken cancellationToken = default);

    /// <summary>FR-CAT-004: Lấy danh mục theo ID.</summary>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-CAT-002: Lấy danh mục theo Slug — CHỈ danh mục, không kèm recipe. Recipe của danh mục lấy theo
    /// trang qua <see cref="IRecipeRepository.GetPagedByCategoryIdAsync"/> (NFR-PERF-004).
    /// </summary>
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra tồn tại theo tên danh mục.</summary>
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>FR-CAT-004: Kiểm tra tồn tại danh mục KHÁC có cùng tên (khi cập nhật).</summary>
    Task<bool> ExistsByNameAsync(string name, Guid excludeId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra tồn tại theo Slug.</summary>
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>FR-CAT-005 / D2: danh mục còn recipe chưa xóa mềm không.</summary>
    Task<bool> HasRecipesAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>FR-CAT-005: Đếm mọi recipe chưa xóa mềm (kể cả Draft, Archived).</summary>
    Task<int> GetTotalRecipeCountAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>Đếm số công thức đã xuất bản trong danh mục.</summary>
    Task<int> GetPublishedRecipeCountAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>FR-CAT-003: Thêm danh mục mới.</summary>
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>FR-CAT-004: Cập nhật thông tin danh mục.</summary>
    void Update(Category category);

    /// <summary>FR-CAT-005: Xóa mềm danh mục (Soft Delete).</summary>
    void Delete(Category category);
}

/// <summary>FR-CAT-001 — một dòng danh mục kèm số công thức Published (read model, không phải entity).</summary>
public sealed record CategorySummary(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int PublishedRecipeCount);
