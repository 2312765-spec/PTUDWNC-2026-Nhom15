namespace CulinaryBlog.Application.Common.Files;

/// <summary>
/// FR-FILE-001/NFR-SEC-004 — sinh tên object an toàn cho MinIO: {folder}/{Guid}{extension}.
/// Extension giới hạn đúng 4 định dạng CONS-007 cho phép — không nhận đuôi tùy ý từ caller,
/// vừa chặn path traversal vừa là lớp phòng thủ thứ hai nếu ai đó bỏ qua validator.
/// </summary>
public static class ObjectKey
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".png", ".webp", ".avif"];

    public static string Create(string folder, string extension)
    {
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"Extension '{extension}' không hợp lệ.", nameof(extension));
        }

        if (string.IsNullOrWhiteSpace(folder) ||
            folder.Contains("..", StringComparison.Ordinal) ||
            folder.StartsWith('/') ||
            folder.Contains('\\'))
        {
            throw new ArgumentException($"Folder '{folder}' không hợp lệ.", nameof(folder));
        }

        return $"{folder}/{Guid.NewGuid()}{extension}";
    }
}
