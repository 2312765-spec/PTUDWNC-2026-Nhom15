using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

/// <summary>
/// FR-RCP-008 (D) chỉ cần một method duy nhất để nạp Recipe kèm Images. FR-RCP-001..007 (C, S7)
/// sẽ bổ sung thêm method (GetBySlugAsync, danh sách có filter...) khi tới lượt — không đổi gì
/// ở đây, chỉ thêm, để tránh giẫm chân giữa hai nhánh làm song song (xem team-assignment.md mục 5).
/// </summary>
public interface IRecipeRepository
{
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
}
