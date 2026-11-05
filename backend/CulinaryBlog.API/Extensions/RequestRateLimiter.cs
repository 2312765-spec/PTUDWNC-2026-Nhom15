using System.Threading.RateLimiting;

namespace CulinaryBlog.API.Extensions;

/// <summary>NFR-SEC-003/D15 — ba nhóm giới hạn, mỗi nhóm một bộ đếm riêng theo IP.</summary>
public enum RateLimitGroup
{
    /// <summary>Không giới hạn (health check của orchestrator).</summary>
    Exempt,
    /// <summary><c>/api/v1/auth/*</c> — Fixed Window, mặc định 10 req/phút/IP.</summary>
    Auth,
    /// <summary><c>POST /api/v1/recipes/{id}/images</c> — Fixed Window, mặc định 5 req/phút/IP.</summary>
    Upload,
    /// <summary>Mọi request còn lại — Sliding Window, mặc định 100 req/phút/IP.</summary>
    General,
}

/// <summary>Một request cần được đếm: nhóm giới hạn + IP máy khách.</summary>
public readonly record struct RateLimitRequest(RateLimitGroup Group, string ClientIp);

/// <summary>Kết quả xin một lượt: được/không, hạn mức, còn lại, giây chờ nếu bị từ chối.</summary>
public readonly record struct RateLimitDecision(bool Allowed, int Limit, long Remaining, int RetryAfterSeconds);

/// <summary>
/// NFR-SEC-003/D15 — bộ đếm rate limit. Nhóm Auth/Upload dùng Fixed Window, nhóm General dùng
/// Sliding Window (D15). Không xếp hàng chờ (QueueLimit = 0): vượt là từ chối ngay bằng 429.
/// Tự gọi <see cref="PartitionedRateLimiter"/> thay vì <c>UseRateLimiter()</c> có sẵn: D15 đòi
/// <c>X-RateLimit-*</c> trên MỌI response, mà <c>UseRateLimiter</c> chỉ cho can thiệp lúc từ chối (D63).
/// </summary>
public sealed class RequestRateLimiter(RateLimitingOptions options) : IDisposable
{
    private readonly TimeSpan _window = TimeSpan.FromSeconds(options.WindowSeconds);

    private readonly PartitionedRateLimiter<RateLimitRequest> _limiter =
        PartitionedRateLimiter.Create<RateLimitRequest, string>(request =>
        {
            var window = TimeSpan.FromSeconds(options.WindowSeconds);
            var key = $"{request.Group}:{request.ClientIp}";

            return request.Group switch
            {
                RateLimitGroup.Exempt => RateLimitPartition.GetNoLimiter(key),
                RateLimitGroup.General => RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = options.General.PermitLimit,
                    Window = window,
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }),
                _ => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = request.Group == RateLimitGroup.Auth ? options.Auth.PermitLimit : options.Upload.PermitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }),
            };
        });

    public int WindowSeconds => options.WindowSeconds;

    /// <summary>Phân loại request. Khớp theo từng đoạn đường dẫn nên <c>/api/v1/authx</c> không bị tính là auth.</summary>
    public static RateLimitGroup Classify(string method, PathString path)
    {
        if (path.StartsWithSegments("/health")) return RateLimitGroup.Exempt;
        if (path.StartsWithSegments("/api/v1/auth")) return RateLimitGroup.Auth;

        if (HttpMethods.IsPost(method) && IsRecipeImagesPath(path.Value))
        {
            return RateLimitGroup.Upload;
        }

        return RateLimitGroup.General;
    }

    // /api/v1/recipes/{id}/images  (cho phép dấu "/" cuối)
    private static bool IsRecipeImagesPath(string? path)
    {
        if (path is null) return false;
        var segments = path.Trim('/').Split('/');
        return segments is ["api", "v1", "recipes", _, "images"];
    }

    public RateLimitDecision? TryAcquire(RateLimitRequest request)
    {
        if (request.Group == RateLimitGroup.Exempt) return null;

        using var lease = _limiter.AttemptAcquire(request);
        var limit = request.Group switch
        {
            RateLimitGroup.Auth => options.Auth.PermitLimit,
            RateLimitGroup.Upload => options.Upload.PermitLimit,
            _ => options.General.PermitLimit,
        };

        if (lease.IsAcquired)
        {
            var remaining = _limiter.GetStatistics(request)?.CurrentAvailablePermits ?? 0;
            return new RateLimitDecision(true, limit, remaining, 0);
        }

        var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : _window;
        return new RateLimitDecision(false, limit, 0, Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)));
    }

    public void Dispose() => _limiter.Dispose();
}
