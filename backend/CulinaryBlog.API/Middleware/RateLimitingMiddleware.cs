using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Middleware;

/// <summary>
/// NFR-SEC-003/D15 — rate limit theo IP. Vượt giới hạn → 429 RFC 7807 <c>RATE_LIMIT_EXCEEDED</c> +
/// <c>Retry-After</c>. Mọi response (kể cả thành công) kèm <c>X-RateLimit-Limit/Remaining/Reset</c>.
/// Đặt sau CORS (429 vẫn có header CORS để trình duyệt đọc được) và trước xác thực.
/// </summary>
public sealed class RateLimitingMiddleware(
    RequestDelegate next,
    RequestRateLimiter limiter,
    ILogger<RateLimitingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var group = RequestRateLimiter.Classify(context.Request.Method, context.Request.Path);
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var decision = limiter.TryAcquire(new RateLimitRequest(group, clientIp));
        if (decision is not { } result)
        {
            await next(context);
            return;
        }

        // Reset = thời điểm cửa sổ tính lại; với cửa sổ trượt đây là cận trên (xấp xỉ).
        var resetSeconds = result.Allowed ? limiter.WindowSeconds : result.RetryAfterSeconds;
        var headers = context.Response.Headers;
        headers["X-RateLimit-Limit"] = result.Limit.ToString();
        headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddSeconds(resetSeconds).ToUnixTimeSeconds().ToString();

        if (result.Allowed)
        {
            await next(context);
            return;
        }

        logger.LogWarning("{ErrorCode} (429): nhóm {Group}, IP {ClientIp}, chờ {RetryAfter}s",
            ErrorCodes.RateLimitExceeded, group, clientIp, result.RetryAfterSeconds);

        headers.RetryAfter = result.RetryAfterSeconds.ToString();

        var problem = new ProblemDetails
        {
            Type = ErrorCodes.RateLimitExceeded,
            Title = "Quá nhiều yêu cầu",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = $"Vượt giới hạn yêu cầu. Thử lại sau {result.RetryAfterSeconds} giây.",
            Instance = context.Request.Path,
        };

        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        // Truyền contentType vào WriteAsJsonAsync: gán Response.ContentType trước đó sẽ bị ghi đè thành application/json.
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
