using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Domain.Interfaces;

/// <summary>
/// Repository interface cho Recipe Aggregate Root.
/// Thuộc Tầng Domain (Domain Layer).
/// </summary>
public interface IRecipeRepository
{
<<<<<<< HEAD
    /// <summary>
    /// Lấy chi tiết công thức kèm Images, Steps, Ingredients theo ID.
    /// </summary>
    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Lấy chi tiết công thức kèm Images, Steps, Ingredients theo Slug.
    /// </summary>
    Task<Recipe?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách công thức thuộc danh mục có lọc theo trạng thái và phân trang.
    /// </summary>
    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedByCategoryIdAsync(
        Guid categoryId,
        RecipeStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra tồn tại theo Slug.
    /// </summary>
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm công thức mới.
    /// </summary>
    Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật thông tin công thức.
    /// </summary>
    void Update(Recipe recipe);

    /// <summary>
    /// Xóa mềm hoặc xóa cứng công thức.
    /// </summary>
    void Delete(Recipe recipe);
=======
    /// <summary>Có tracking (không AsNoTracking) — dùng để mutate rồi SaveChanges.</summary>
    Task<Recipe?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Chỉ đọc AuthorId, không tracking — để kiểm tra quyền TRƯỚC khi làm việc tốn kém (upload MinIO)
    /// mà không giữ entity đã nạp. Trả <c>null</c> nếu recipe không tồn tại (hoặc đã soft delete).
    /// </summary>
    Task<string?> GetAuthorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Như <see cref="GetByIdWithImagesAsync"/> nhưng khoá dòng Recipe (<c>FOR UPDATE</c>) tới hết
    /// transaction, để các request cùng sửa bộ ảnh của một recipe xếp hàng thay vì cùng đọc "chưa
    /// có ảnh" rồi cùng đặt mình làm primary (vi phạm unique index D27). PHẢI gọi trong
    /// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/> — ngoài transaction, khoá nhả ngay.
    /// </summary>
    Task<Recipe?> GetByIdWithImagesForUpdateAsync(Guid id, CancellationToken ct = default);
>>>>>>> main
}
