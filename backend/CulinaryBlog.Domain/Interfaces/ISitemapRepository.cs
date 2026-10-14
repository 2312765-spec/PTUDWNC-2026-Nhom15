namespace CulinaryBlog.Domain.Interfaces;

/// <summary>FR-JOB-003/D52 — một URL nội dung trong sitemap: slug + thời điểm sửa gần nhất (UTC).</summary>
public sealed record SitemapEntry(string Slug, DateTime LastModified);

/// <summary>
/// FR-JOB-003/D52 — đọc dữ liệu cho sitemap.xml. Interface riêng (không thêm vào IRecipeRepository/
/// ICategoryRepository) vì chỉ cần slug + lastmod, không cần aggregate.
/// Bản ghi đã soft delete bị Global Query Filter loại (D1, D2).
/// </summary>
public interface ISitemapRepository
{
    /// <summary>Chỉ recipe Published. LastModified = UpdatedAt ?? PublishedAt ?? CreatedAt.</summary>
    Task<IReadOnlyList<SitemapEntry>> GetPublishedRecipeEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Mọi category chưa xóa. LastModified = UpdatedAt ?? CreatedAt.</summary>
    Task<IReadOnlyList<SitemapEntry>> GetCategoryEntriesAsync(CancellationToken cancellationToken = default);
}
