namespace CulinaryBlog.Application.Seo;

/// <summary>NFR-SEO-003 — một phần tử &lt;url&gt; của sitemap.xml.</summary>
public sealed record SitemapUrl(string Loc, DateTime? LastModified, string ChangeFrequency, decimal Priority);
