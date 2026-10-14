using CulinaryBlog.Domain.Interfaces;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

/// <summary>FR-JOB-003/D52 — projection chỉ lấy slug + lastmod, AsNoTracking (NFR-PERF-004).</summary>
public sealed class SitemapRepository(CulinaryBlogDbContext dbContext) : ISitemapRepository
{
    public Task<IReadOnlyList<SitemapEntry>> GetPublishedRecipeEntriesAsync(CancellationToken cancellationToken = default)
    {
        _ = dbContext;
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<SitemapEntry>> GetCategoryEntriesAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
