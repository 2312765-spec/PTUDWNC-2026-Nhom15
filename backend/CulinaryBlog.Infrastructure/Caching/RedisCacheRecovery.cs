using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CulinaryBlog.Infrastructure.Caching;

/// <summary>
/// NFR-REL-002 / D90 — dọn toàn bộ cache khi API kết nối lại được Redis.
///
/// Trong lúc Redis tắt, lệnh ghi vẫn thành công nhưng xóa tag thất bại (CacheInvalidationBehavior
/// nuốt lỗi). Redis lên lại với dữ liệu cũ (persistence) thì API sẽ trả bản cũ tới hết TTL —
/// <c>categories:all</c> tới 30 phút. Không biết lệnh nào đã lỡ xóa cache, nên xóa hết: quét mọi tag
/// set <c>tag:*</c> (mọi key cache đều có ít nhất một tag — D8) rồi xóa key trong đó.
///
/// Không giữ danh sách "tag đã lỡ xóa" trong bộ nhớ: nhiều instance API thì instance khác cũng phải
/// dọn được, và D8 / NFR-SCALE-001 yêu cầu API stateless.
/// </summary>
public sealed class RedisCacheRecovery
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheRecovery> _logger;

    private RedisCacheRecovery(IConnectionMultiplexer redis, ILogger<RedisCacheRecovery> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <summary>Gắn vào sự kiện <c>ConnectionRestored</c> của multiplexer. Gọi một lần khi tạo multiplexer.</summary>
    public static void Attach(IConnectionMultiplexer redis, ILogger<RedisCacheRecovery> logger)
    {
        var recovery = new RedisCacheRecovery(redis, logger);
        redis.ConnectionRestored += recovery.OnConnectionRestored;
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        // Mỗi endpoint có 2 kết nối (Interactive + Subscription); chỉ cache dùng Interactive.
        if (e.ConnectionType != ConnectionType.Interactive || e.EndPoint is not { } endPoint)
        {
            return;
        }

        // Event handler chạy trên luồng của StackExchange.Redis — không chặn nó.
        _ = Task.Run(() => FlushAllTaggedKeysAsync(endPoint));
    }

    /// <summary>Xóa mọi key cache trên một endpoint. Không bao giờ throw (NFR-REL-002).</summary>
    public async Task FlushAllTaggedKeysAsync(System.Net.EndPoint endPoint)
    {
        try
        {
            var server = _redis.GetServer(endPoint);
            if (server.IsReplica)
            {
                return;
            }

            var db = _redis.GetDatabase();
            var removed = 0;

            await foreach (var tagKey in server.KeysAsync(db.Database, pattern: $"{RedisCacheService.TagKeyPrefix}*"))
            {
                await RedisCacheService.RemoveTagSetAsync(db, tagKey);
                removed++;
            }

            _logger.LogWarning(
                "Kết nối lại Redis {EndPoint} — đã xóa {TagCount} tag cache để không trả dữ liệu cũ (D90)",
                endPoint,
                removed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dọn cache sau khi kết nối lại Redis {EndPoint} thất bại — cache sẽ tự hết hạn theo TTL", endPoint);
        }
    }
}
