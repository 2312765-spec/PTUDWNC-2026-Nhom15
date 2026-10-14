using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Seo.Commands.GenerateSitemap;

/// <summary>FR-JOB-003, NFR-SEO-003, D51–D53.</summary>
public sealed class GenerateSitemapCommandHandler(
    ISitemapRepository sitemapRepository,
    IFileStorageService fileStorageService,
    ILogger<GenerateSitemapCommandHandler> logger)
    : IRequestHandler<GenerateSitemapCommand, int>
{
    /// <summary>D52 — key cố định trên MinIO; Nginx phục vụ tại /sitemap.xml.</summary>
    public const string ObjectKey = "seo/sitemap.xml";

    public const string ContentType = "application/xml";

    public Task<int> Handle(GenerateSitemapCommand request, CancellationToken cancellationToken)
    {
        _ = (sitemapRepository, fileStorageService, logger);
        throw new NotImplementedException();
    }
}
