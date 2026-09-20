namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// HỢP ĐỒNG CHUNG — chủ sở hữu: B (viết ở Sprint 1). C dùng cho Recipe.
/// D10: bỏ dấu tiếng Việt → lowercase → ký tự lạ thành "-" → auto-suffix "-2", "-3" khi trùng.
/// Slug cấm: "search", "new", "edit" (tránh đụng route).
/// Ví dụ: "Phở Bò Tái Nạm" → "pho-bo-tai-nam".
/// </summary>
public interface ISlugHelper
{
    string Generate(string text);

    /// <summary>Thêm hậu tố cho tới khi <paramref name="isUnique"/> trả true.</summary>
    Task<string> GenerateUniqueAsync(string text, Func<string, CancellationToken, Task<bool>> isUnique, CancellationToken ct = default);
}
