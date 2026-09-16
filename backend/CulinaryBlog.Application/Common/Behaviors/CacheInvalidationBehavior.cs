using CulinaryBlog.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Behaviors;

/// <summary>
/// Pipeline #5 (SRS 6.3) — chạy SAU handler, chỉ cho Command implements <see cref="ICacheInvalidator"/>.
/// D8: Command trên Category phải xóa CẢ tag "categories" VÀ "recipes".
/// </summary>
public sealed class CacheInvalidationBehavior<TRequest, TResponse>(
    ICacheService cache,
    ILogger<CacheInvalidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is ICacheInvalidator invalidator && invalidator.TagsToInvalidate.Count > 0)
        {
            try
            {
                await cache.RemoveByTagsAsync(invalidator.TagsToInvalidate, cancellationToken);
                logger.LogDebug("Đã xóa cache theo tag: {Tags}", string.Join(", ", invalidator.TagsToInvalidate));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Xóa cache thất bại — dữ liệu ĐÃ ghi thành công, cache sẽ tự hết hạn theo TTL");
            }
        }

        return response;
    }
}
