using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

/// <summary>
/// FR-JOB-003/D52 — projection chỉ lấy slug + lastmod, AsNoTracking (NFR-PERF-004).
/// Global Query Filter IsDeleted loại bản ghi đã soft delete (D1, D2). Sắp theo slug để file ổn định giữa các lần chạy.
/// </summary>
public sealed class SitemapRepository(CulinaryBlogDbContext dbContext) : ISitemapRepository
{
    public async Task<IReadOnlyList<SitemapEntry>> GetPublishedRecipeEntriesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.Status == RecipeStatus.Published)
            .OrderBy(r => r.Slug)
            .Select(r => new SitemapEntry(r.Slug, r.UpdatedAt ?? r.PublishedAt ?? r.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SitemapEntry>> GetCategoryEntriesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Slug)
            .Select(c => new SitemapEntry(c.Slug, c.UpdatedAt ?? c.CreatedAt))
            .ToListAsync(cancellationToken);
}
