using MediatR;

namespace CulinaryBlog.Application.Seo.Commands.GenerateSitemap;

/// <summary>
/// FR-JOB-003/D52 — sinh sitemap.xml và lưu lên MinIO. Trả về số URL trong sitemap.
/// <paramref name="SiteBaseUrl"/> là gốc site công khai (Seo:SiteBaseUrl), job đọc cấu hình rồi truyền vào.
/// Không implement ICacheInvalidator — không cache nào phụ thuộc sitemap.
/// </summary>
public sealed record GenerateSitemapCommand(string SiteBaseUrl) : IRequest<int>;
