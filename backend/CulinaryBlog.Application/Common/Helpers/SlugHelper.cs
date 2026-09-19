using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Common.Helpers;

/// <summary>
/// Helper sinh Slug thân thiện SEO theo đặc tả SRS FR-CAT-003 Luồng chính bước 5
/// Quy tắc: Chuyển sang chữ thường, bỏ dấu tiếng Việt, thay khoảng trắng bằng "-"
/// </summary>
public static partial class SlugHelper
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleDashesRegex();

    public static string Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // 1. Chuyển chữ thường
        var normalizedText = text.Trim().ToLowerInvariant();

        // 2. Chuẩn hóa ký tự Đ / đ tiếng Việt trước khi unaccent
        normalizedText = normalizedText.Replace("đ", "d").Replace("Đ", "d");

        // 3. Loại bỏ dấu tiếng Việt (Unicode Normalization Form D)
        var normalizedString = normalizedText.Normalize(NormalizationForm.FormD);
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

        // 4. Thay thế ký tự không phải chữ cái, số, dấu cách hoặc gạch ngang
        cleanText = InvalidCharsRegex().Replace(cleanText, "");

        // 5. Thay khoảng trắng thành dấu gạch ngang
        cleanText = SpaceRegex().Replace(cleanText, "-");

        // 6. Rút gọn nhiều dấu gạch ngang liên tiếp thành 1 dấu
        cleanText = MultipleDashesRegex().Replace(cleanText, "-");

        // 7. Cắt bỏ dấu gạch ngang ở đầu và cuối chuỗi
        return cleanText.Trim('-');
    }
}
