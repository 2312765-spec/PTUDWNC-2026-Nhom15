using System.Text.Json;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CulinaryBlog.Infrastructure.Caching;

/// <summary>
/// D8 — hiện thực cache DUY NHẤT của hệ thống. Không dùng IMemoryCache, không dùng Output Cache.
///
/// Tag hoạt động bằng Redis SET: mỗi tag là một set chứa danh sách cache key.
/// Xóa tag = đọc set, xóa tất cả key trong đó, rồi xóa chính set.
///
/// NFR-REL-002: mọi lỗi Redis đều được nuốt và log warning — không bao giờ throw lên trên.
/// </summary>
public sealed class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string TagKey(string tag) => $"tag:{tag}";

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            return value.IsNullOrEmpty
                ? default
                : JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis GET lỗi cho key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, IEnumerable<string> tags, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var payload = JsonSerializer.Serialize(value, JsonOptions);

            var batch = db.CreateBatch();
            var tasks = new List<Task> { batch.StringSetAsync(key, payload, ttl) };

            foreach (var tag in tags)
            {
                tasks.Add(batch.SetAddAsync(TagKey(tag), key));
            }

            batch.Execute();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis SET lỗi cho key {Key}", key);
        }
    }

    public async Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();

            foreach (var tag in tags)
            {
                var tagKey = TagKey(tag);
                var members = await db.SetMembersAsync(tagKey);

                if (members.Length > 0)
                {
                    await db.KeyDeleteAsync([.. members.Select(m => (RedisKey)m.ToString())]);
                }

                await db.KeyDeleteAsync(tagKey);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis xóa theo tag lỗi — cache sẽ tự hết hạn theo TTL");
        }
    }
}
