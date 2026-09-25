using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.Infrastructure.Services;

public class SlugHelper : ISlugHelper
{
    private static readonly HashSet<string> _reservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "search", "new", "edit"
    };

    public string Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) 
        {return string.Empty;}

        // D10: Xử lý đ, Đ trong tiếng Việt
        var replaced = text.Replace("đ", "d").Replace("Đ", "d");

        // Bỏ dấu (FormD)
        var normalizedString = replaced.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var clean = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // Xóa ký tự lạ, khoảng trắng thành '-'
        clean = Regex.Replace(clean, @"[^a-z0-9\s-]", "");
        clean = Regex.Replace(clean, @"\s+", "-").Trim('-');

        // D10: Slug cấm thêm -1
        if (_reservedSlugs.Contains(clean))
        {
            clean = $"{clean}-1";
        }

        return clean;
    }

    public async Task<string> GenerateUniqueAsync(
        string text, 
        Func<string, CancellationToken, Task<bool>> existsFunc, 
        CancellationToken cancellationToken = default)
    {
        var baseSlug = Generate(text);
        var finalSlug = baseSlug;
        var suffix = 2;

        while (await existsFunc(finalSlug, cancellationToken))
        {
            finalSlug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return finalSlug;
    }
}