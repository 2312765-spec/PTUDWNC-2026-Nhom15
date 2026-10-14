namespace CulinaryBlog.Application.Seo;

/// <summary>NFR-SEO-003 — dựng sitemap.xml theo sitemaps.org 0.9.</summary>
public static class SitemapXmlBuilder
{
    public const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static byte[] Build(IEnumerable<SitemapUrl> urls) => throw new NotImplementedException();
}
