using System.Reflection;

namespace CulinaryBlog.Infrastructure.Persistence.Seeding;

/// <summary>
/// DEV-SEED — ảnh món ăn cho dữ liệu mẫu Development, nhúng vào assembly (<c>Images/{slug}.webp</c>, 1200×900).
/// Nguồn Wikimedia Commons, ghi công ở <c>Persistence/Seeding/Images/CREDITS.md</c>. Tên file = slug sinh từ tên món
/// (<c>ISlugHelper.Generate(title)</c>). Món chưa tìm được ảnh có giấy phép phù hợp thì không có file.
/// Plan: docs/plans/DEV-SEED-anh-mon-an.md.
/// </summary>
public static class SeedImageAssets
{
    private const string Prefix = "SeedImages/";
    private const string Extension = ".webp";

    public const string ContentType = "image/webp";

    private static readonly Assembly Assembly = typeof(SeedImageAssets).Assembly;

    /// <summary>Slug các món có ảnh.</summary>
    public static IReadOnlySet<string> Slugs { get; } = Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(Extension, StringComparison.Ordinal))
        .Select(name => name[Prefix.Length..^Extension.Length])
        .ToHashSet(StringComparer.Ordinal);

    public static string FileName(string slug) => slug + Extension;

    /// <summary>Stream ảnh của món, hoặc null nếu món chưa có ảnh. Bên gọi dispose.</summary>
    public static Stream? Open(string slug) => Assembly.GetManifestResourceStream(Prefix + FileName(slug));
}
