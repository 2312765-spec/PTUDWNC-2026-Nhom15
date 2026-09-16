using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Behaviors;

/// <summary>
/// Pipeline #1 (SRS 6.3) — log mọi Command và Query.
/// CONS-010 + FR-OBS-002: cảnh báo khi request > 500ms.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogInformation("Bắt đầu xử lý {RequestName}", requestName);

        try
        {
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                logger.LogWarning(
                    "{RequestName} CHẬM: {ElapsedMs}ms (ngưỡng {Threshold}ms)",
                    requestName, sw.ElapsedMilliseconds, SlowRequestThresholdMs);
            }
            else
            {
                logger.LogInformation("{RequestName} xong trong {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "{RequestName} lỗi sau {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
