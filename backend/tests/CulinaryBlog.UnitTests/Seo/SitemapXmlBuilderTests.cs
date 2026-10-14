using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CulinaryBlog.Application.Seo;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Seo;

/// <summary>NFR-SEO-003 — định dạng sitemap.xml theo sitemaps.org 0.9.</summary>
public class SitemapXmlBuilderTests
{
    private static readonly XNamespace Ns = SitemapXmlBuilder.Namespace;

    [Fact(DisplayName = "NFR-SEO-003: urlset đúng namespace, mỗi SitemapUrl thành một <url> đủ 4 phần tử, giữ thứ tự")]
    public void Build_WritesUrlsetWithAllElements()
    {
        var doc = Parse(SitemapXmlBuilder.Build(
        [
            new SitemapUrl("https://site.test/", new DateTime(2026, 10, 1, 2, 0, 0, DateTimeKind.Utc), "daily", 1.0m),
            new SitemapUrl("https://site.test/recipes/pho-bo", new DateTime(2026, 9, 30, 23, 5, 9, DateTimeKind.Utc), "weekly", 0.8m),
        ]));

        doc.Root!.Name.Should().Be(Ns + "urlset");
        var urls = doc.Root.Elements(Ns + "url").ToList();
        urls.Should().HaveCount(2);

        urls[0].Element(Ns + "loc")!.Value.Should().Be("https://site.test/");
        urls[0].Element(Ns + "lastmod")!.Value.Should().Be("2026-10-01T02:00:00Z");
        urls[0].Element(Ns + "changefreq")!.Value.Should().Be("daily");
        urls[0].Element(Ns + "priority")!.Value.Should().Be("1.0");

        urls[1].Element(Ns + "lastmod")!.Value.Should().Be("2026-09-30T23:05:09Z");
        urls[1].Element(Ns + "priority")!.Value.Should().Be("0.8");
    }

    [Fact(DisplayName = "NFR-SEO-003: LastModified null → bỏ <lastmod>, các phần tử khác vẫn có")]
    public void Build_NullLastModified_OmitsLastmod()
    {
        var url = Parse(SitemapXmlBuilder.Build([new SitemapUrl("https://site.test/categories", null, "weekly", 0.7m)]))
            .Root!.Element(Ns + "url")!;

        url.Element(Ns + "lastmod").Should().BeNull();
        url.Element(Ns + "changefreq")!.Value.Should().Be("weekly");
        url.Element(Ns + "priority")!.Value.Should().Be("0.7");
    }

    [Fact(DisplayName = "NFR-SEO-003: ký tự đặc biệt XML (&, <) trong loc được escape — file vẫn parse được")]
    public void Build_EscapesXmlSpecialCharacters()
    {
        const string loc = "https://site.test/recipes/a?b=1&c=<2>";

        var bytes = SitemapXmlBuilder.Build([new SitemapUrl(loc, null, "weekly", 0.8m)]);

        Encoding.UTF8.GetString(bytes).Should().Contain("&amp;").And.NotContain("&c=<2>");
        Parse(bytes).Root!.Element(Ns + "url")!.Element(Ns + "loc")!.Value.Should().Be(loc);
    }

    [Fact(DisplayName = "NFR-SEO-003: priority luôn dùng dấu chấm, không phụ thuộc culture máy chạy (vi-VN dùng dấu phẩy)")]
    public void Build_PriorityIsCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
        try
        {
            var url = Parse(SitemapXmlBuilder.Build([new SitemapUrl("https://site.test/", null, "daily", 0.9m)]))
                .Root!.Element(Ns + "url")!;

            url.Element(Ns + "priority")!.Value.Should().Be("0.9");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "NFR-SEO-003: UTF-8 không BOM, có khai báo <?xml ... encoding=\"utf-8\"?>")]
    public void Build_Utf8WithoutBom()
    {
        var bytes = SitemapXmlBuilder.Build([]);

        bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
        Encoding.UTF8.GetString(bytes).Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    }

    private static XDocument Parse(byte[] bytes) => XDocument.Load(new MemoryStream(bytes));
}
