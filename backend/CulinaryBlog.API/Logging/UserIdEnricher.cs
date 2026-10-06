using System.Security.Claims;
using Serilog.Core;
using Serilog.Events;

namespace CulinaryBlog.API.Logging;

/// <summary>
/// CONS-010 + FR-OBS-002 — gắn <c>UserId</c> vào mọi log entry khi request đã xác thực.
/// Đọc <c>HttpContext.User</c> tại thời điểm ghi log (không push vào LogContext ở một middleware),
/// nên không phụ thuộc thứ tự middleware: log ghi sau <c>UseAuthentication</c> — kể cả request log
/// và log lỗi của GlobalExceptionMiddleware — đều có UserId.
/// </summary>
public sealed class UserIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public const string PropertyName = "UserId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, userId));
        }
    }
}
