using CulinaryBlog.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Behaviors;

/// <summary>
/// Pipeline #3 (SRS 6.3) — chỉ chạy cho Query implements <see cref="ICacheable"/>.
/// D8: CHỈ Redis. Redis lỗi thì đi thẳng xuống handler, KHÔNG throw (NFR-REL-002).
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>(
    ICacheService cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheable cacheable)
        {
            return await next();
        }

        try
        {
            var cached = await cache.GetAsync<TResponse>(cacheable.CacheKey, cancellationToken);
            if (cached is not null)
            {
                logger.LogDebug("Cache HIT {CacheKey}", cacheable.CacheKey);
                return cached;
            }
        }
        catch (Exception ex)
        {
            // NFR-REL-002: Redis down không được làm sập request.
            logger.LogWarning(ex, "Đọc cache thất bại cho {CacheKey} — bỏ qua cache", cacheable.CacheKey);
            return await next();
        }

        logger.LogDebug("Cache MISS {CacheKey}", cacheable.CacheKey);
        var response = await next();

        try
        {
            await cache.SetAsync(cacheable.CacheKey, response, cacheable.CacheTtl, cacheable.CacheTags, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ghi cache thất bại cho {CacheKey}", cacheable.CacheKey);
        }

        return response;
    }
}
