using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Seo.Commands.GenerateSitemap;

/// <summary>
/// FR-JOB-003, NFR-SEO-003 — D51 (không ping Google), D52 (danh sách URL, key cố định trên MinIO).
/// Lệnh hệ thống, không có endpoint gọi tới → không kiểm tra quyền.
/// </summary>
public sealed class GenerateSitemapCommandHandler(
    ISitemapRepository sitemapRepository,
    IFileStorageService fileStorageService,
    ILogger<GenerateSitemapCommandHandler> logger)
    : IRequestHandler<GenerateSitemapCommand, int>
{
    /// <summary>D52 — key cố định trên MinIO; Nginx phục vụ tại /sitemap.xml.</summary>
    public const string ObjectKey = "seo/sitemap.xml";

    public const string ContentType = "application/xml";

    public async Task<int> Handle(GenerateSitemapCommand request, CancellationToken cancellationToken)
    {
        var recipes = await sitemapRepository.GetPublishedRecipeEntriesAsync(cancellationToken);
        var categories = await sitemapRepository.GetCategoryEntriesAsync(cancellationToken);

        var baseUrl = request.SiteBaseUrl.TrimEnd('/');
        var latestRecipe = Latest(recipes);

        List<SitemapUrl> urls =
        [
            new($"{baseUrl}/", latestRecipe, "daily", 1.0m),
            new($"{baseUrl}/recipes", latestRecipe, "daily", 0.9m),
            new($"{baseUrl}/categories", Latest(categories), "weekly", 0.7m),
            .. categories.Select(c => new SitemapUrl($"{baseUrl}/categories/{c.Slug}", c.LastModified, "weekly", 0.7m)),
            .. recipes.Select(r => new SitemapUrl($"{baseUrl}/recipes/{r.Slug}", r.LastModified, "weekly", 0.8m)),
        ];

        using var content = new MemoryStream(SitemapXmlBuilder.Build(urls));
        var url = await fileStorageService.SaveAsync(ObjectKey, content, ContentType, cancellationToken);

        logger.LogInformation(
            "FR-JOB-003: sinh sitemap {UrlCount} URL ({RecipeCount} recipe, {CategoryCount} category) → {SitemapUrl}",
            urls.Count, recipes.Count, categories.Count, url);

        return urls.Count;
    }

    private static DateTime? Latest(IReadOnlyList<SitemapEntry> entries) =>
        entries.Count == 0 ? null : entries.Max(e => e.LastModified);
}
