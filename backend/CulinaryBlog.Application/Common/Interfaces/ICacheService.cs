namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// HỢP ĐỒNG CHUNG — chủ sở hữu: B. Chốt tuần 1.
/// D8: CHỈ Redis. Cấm IMemoryCache và ASP.NET Output Cache.
/// Redis down KHÔNG được làm sập request — implementation phải nuốt lỗi và log warning.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, IEnumerable<string> tags, CancellationToken ct = default);
    Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default);
}

/// <summary>Query gắn interface này sẽ được CachingBehavior xử lý.</summary>
public interface ICacheable
{
    /// <summary>Ví dụ: "recipe:pho-bo", "recipes:list:{hash}".</summary>
    string CacheKey { get; }

    /// <summary>TTL theo bảng D8: categories 30p · recipes:list 15p · recipe detail 5p · search 1p.</summary>
    TimeSpan CacheTtl { get; }

    /// <summary>Tag để invalidate hàng loạt. Ví dụ: ["recipes", "recipe:pho-bo"].</summary>
    IReadOnlyList<string> CacheTags { get; }
}

/// <summary>Command gắn interface này sẽ được CacheInvalidationBehavior xóa cache sau khi chạy xong.</summary>
public interface ICacheInvalidator
{
    /// <summary>
    /// D8 — Command trên Category phải trả về CẢ "categories" VÀ "recipes",
    /// vì DTO recipe có nhúng tên category.
    /// </summary>
    IReadOnlyList<string> TagsToInvalidate { get; }
}
