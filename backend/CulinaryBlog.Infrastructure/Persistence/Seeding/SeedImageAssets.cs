using System.Reflection;
using System.Security.Cryptography;

namespace CulinaryBlog.Infrastructure.Persistence.Seeding;

/// <summary>
/// DEV-SEED — ảnh món ăn cho dữ liệu mẫu Development, nhúng vào assembly (<c>Images/{slug}.webp</c>, 1200×900).
/// D98: nguồn kết hợp Pexels / Unsplash / Wikimedia Commons, danh sách ở <c>Persistence/Seeding/Images/CREDITS.md</c>.
/// Tên file = slug sinh từ tên món (<c>ISlugHelper.Generate(title)</c>). Món chưa tìm được ảnh đúng món thì không có file.
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

    /// <summary>
    /// D98 — SHA-256 (hex thường) của ảnh seed bản Wikimedia đã bị thay. DB dev đã seed trước D98 còn giữ các ảnh này;
    /// <see cref="DbSeeder"/> nhận ra chúng theo nội dung (không theo URL/alt) để thay mà không đụng ảnh người dùng upload.
    /// </summary>
    public static IReadOnlySet<string> RetiredSha256 { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "758bc96e11441c7bcd79b92c68dce7d30a69b92025198674f313e603b4b63544", // banh-bot-loc
        "bb134e3217b432ed82970a22bf62eb4861dfcca1ca579e11f4c41a63ba49a949", // banh-canh-cua
        "f8012c588335b1c78938026bddd7ae79a8957feeb091bc9f642238debab03ad6", // banh-flan-caramel
        "601c6d091ca3d4a44438d977734cb82c8c19eae770c5e6f34af85f133abfdeed", // banh-flan-nuong-mini
        "5c345f65bfee1d3e165a89baee47577bdf7d5d3a8e9de5866586a163d810cbcc", // banh-mi-thit
        "0ff7f9041c115cbff2f225041732aaaee6caebb30e1d2d1aebe7b866f4525cdb", // banh-trang-tron
        "9c7eb026f043be6ad72a0cd5e81e938f6a62ba8acd0d419a5e2e8ccf9df7f05f", // bo-luc-lac
        "c8ea2c1b6d96970a48f5b10b9ff7d48b472d467ac80e85c304670ee7b9fb4956", // bo-nuong-la-lot
        "b76ee4e0ccd1a015d20163971dea24700699381229966e429616a42ff2a01137", // bun-bo-hue
        "37de2897a8a4730a833a92299c38d7f72a13c9f57e35584f1c0ff21cf4708d6c", // bun-cha-ha-noi
        "c523de9bcbe14e23182f110470451758db7492a342df79e2dbad5ba541684627", // bun-rieu-cua
        "2d7482d82bc67c2604552ac1a379572b4ee0d6850f5cb00d782e2acf7f7fbcd4", // bun-xao-chay
        "7c8e641fb777658160c27327cc5b930f6c99db388862b548a465ba02818e49b5", // ca-kho-to
        "1801471db7c6035d892b0945f3391fbc813487efad750ad09166036a57beed2a", // ca-phe-trung
        "b185491668116f7d330428a7893baf181d229d0f389f90cc151aca4372e419a1", // canh-chua-ca-loc
        "fa4e67cabf8d069cd3b3a7aff38e9150da967e35975cd02ca91c9a6d2d9ae923", // canh-nam-chay
        "0bd6aa78f0ce8b26056b9a1b1fda954ae4831d3c4bb71d46b8bf1786e2ca5109", // cha-gio-chay
        "e8fbf86cf507f499c863690dd0fff1e1b41ca0cf4dbdf10e67122574ef8ecf29", // cha-gio-re
        "706c5019d990f448e8d3aca20ad2ce28cc64d31bbc47acc1afc40c0489e518d1", // che-chuoi-nuoc-cot-dua
        "726c33e609bf1f1aad59b224916878da7f70ad93cc898d1f69732e8445628c0e", // che-dau-xanh-nuoc-cot-dua
        "545773496ed77d8fe52e2941aeb2fc7b8e7a80d4f64632d527b1f35f37544bec", // com-chien-chay
        "3b7e2a5204a4435cdfcfab0219f7875347422360de3318d12025544de2459551", // com-tam-suon-nuong
        "d3afdaef067cc4bd4fe16ca120b3866ab4611ace57ea8487bafb582bf941aa44", // ga-kho-gung
        "bedb1a9895f08a625f8871dc178a84232ca113ba8c7e16463adaa45481d9440e", // ga-nuong-mat-ong
        "8b9d963f28b57c249ec7e64f0fef9b7c0c06858754d821b166fc4cd59b285edf", // goi-cuon-tom-thit
        "aed4d862a805db5dcb072910c74a4fe35b8dc01eadc70b7b10c6026dba3ed4a5", // hu-tieu-nam-vang
        "8eee17733520cbfcf44f85db04b9d28ddc1f28cc1d4db25b4c3416254ff47e39", // khoai-lang-lac-pho-mai
        "aaafb6fb27f7731f1c4cd4561200ed815bc0562c742ecff0b2e2f67752a39f9d", // mi-quang-ga
        "a737891f527c018e9726d7658c2f673e27dfa23692b2ce524c62c9aa63c4f845", // muc-nuong-sa-te
        "6f9b5af1eb6f0885648c2afc5c1aadd8c9a73a1b71dc70a70bf7632743dd6fcc", // nem-lui-hue
        "a6bf0acbcbd277f5e37f3b98ec730073f17270f8bb105f22a3a874cbeb55c660", // nuoc-chanh-day
        "584ac06923c0c6d2386f1ff761e8cf5b7ce4bc00bc0bc9779156f49cba9cf106", // pho-bo-ha-noi
        "2423688fc4ed7a599561f831ac88c9802efbd63a1614799ee9897ada0a8687fb", // rau-cu-xao-thap-cam
        "d4845419ddd31c245759bf5cee927d8d5e398a14e405e952e6427e81d1704c70", // sinh-to-bo
        "70656786ad61adc7d03fbf5c27b65c341463c31f3d46db7c0631f77716033207", // thit-kho-trung
        "f1f5376c0d262e77008576714e787d941ce27ce00808b2a566af630c7f59ae94", // tra-sua-tran-chau
        "158951767882b49632b280b018a39b0cfa271205079d40056c2b39fe8e0d481d", // tra-tac-mat-ong
    };

    public static string FileName(string slug) => slug + Extension;

    /// <summary>Stream ảnh của món, hoặc null nếu món chưa có ảnh. Bên gọi dispose.</summary>
    public static Stream? Open(string slug) => Assembly.GetManifestResourceStream(Prefix + FileName(slug));

    /// <summary>D98 — nội dung có phải một ảnh seed bản cũ đã bị thay không.</summary>
    public static bool IsRetired(Stream content) =>
        RetiredSha256.Contains(Convert.ToHexStringLower(SHA256.HashData(content)));
}
