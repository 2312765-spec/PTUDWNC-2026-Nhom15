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
}
