using Serilog.Events;

namespace CulinaryBlog.API.Logging;

/// <summary>
/// FR-OBS-002 — mức log cho request log HTTP (<c>UseSerilogRequestLogging</c>):
/// lỗi server/exception → Error, chậm hơn ngưỡng → Warning (performance alert), còn lại Information.
/// </summary>
public static class RequestLogLevel
{
    public const double SlowRequestThresholdMs = 500;

    public static LogEventLevel Get(HttpContext context, double elapsedMs, Exception? exception)
    {
        if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        return elapsedMs > SlowRequestThresholdMs ? LogEventLevel.Warning : LogEventLevel.Information;
    }
}
