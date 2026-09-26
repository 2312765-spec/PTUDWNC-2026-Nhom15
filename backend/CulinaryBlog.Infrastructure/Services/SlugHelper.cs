using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// Triển khai ISlugHelper tuân thủ Hợp đồng chung (Quyết định D10):
/// Bỏ dấu tiếng Việt -> lowercase -> ký tự lạ thành "-" -> auto-suffix "-1", "-2" khi trùng.
/// </summary>
public class SlugHelper : ISlugHelper
{
    public string Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // 1. Chuẩn hóa Unicode loại bỏ dấu tiếng Việt (FormD)
        var normalizedString = text.Trim().Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var cleanText = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

        // 2. Chuyển chữ 'đ' / 'Đ' thành 'd'
        cleanText = cleanText.Replace("đ", "d").Replace("Đ", "D");

        // 3. Chuyển về chữ thường, ký tự lạ thành '-'
        cleanText = cleanText.ToLowerInvariant();
        cleanText = Regex.Replace(cleanText, @"[^a-z0-9\s-]", "");
        cleanText = Regex.Replace(cleanText, @"\s+", "-").Trim('-');

        return cleanText;
    }

    // Method tiện ích nếu có chỗ gọi GenerateSlug
    public string GenerateSlug(string text) => Generate(text);

    public async Task<string> GenerateUniqueAsync(
        string text,
        Func<string, CancellationToken, Task<bool>> isSlugTaken,
        CancellationToken ct = default)
    {
        var baseSlug = Generate(text);
        var slug = baseSlug;
        var suffix = 1;

        while (await isSlugTaken(slug, ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }
}