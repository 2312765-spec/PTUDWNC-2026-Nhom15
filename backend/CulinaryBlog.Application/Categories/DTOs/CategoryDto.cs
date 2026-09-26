namespace CulinaryBlog.Application.Categories.DTOs;

/// <summary>
/// DTO trả về cho Category theo đặc tả FR-CAT-001  Chương 8.2 API Spec.
/// Chứa thông tin danh mục kèm số lượng công thức Published.
/// </summary>
/// <param name="Id">Khóa chính UUID v4</param>
/// <param name="Name">Tên danh mục (ví dụ: "Món khai vị")</param>
/// <param name="Slug">URL-friendly slug duy nhất (ví dụ: "mon-khai-vi")</param>
/// <param name="Description">Mô tả danh mục (tùy chọn)</param>
/// <param name="RecipeCount">Số lượng công thức đã xuất bản (Status = Published, IsDeleted = false)</param>
public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int RecipeCount
);
