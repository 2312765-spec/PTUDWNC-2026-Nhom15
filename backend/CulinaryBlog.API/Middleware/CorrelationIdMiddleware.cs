using Serilog.Context;

namespace CulinaryBlog.API.Middleware;

/// <summary>
/// CONS-010 + FR-OBS-002 — mọi log entry phải có CorrelationId.
/// Nhận từ header X-Correlation-ID nếu client gửi, không thì tự sinh.
/// Luôn trả lại trong response để client trace được.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
                            && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Guid.CreateVersion7().ToString();

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty(ItemKey, correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        using (LogContext.PushProperty("UserId", context.User.Identity?.IsAuthenticated == true
                   ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                   : null))
        {
            await next(context);
        }
    }
}
