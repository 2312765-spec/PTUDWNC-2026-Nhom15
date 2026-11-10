using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// NFR-PERF-004 — "Slow query log: cảnh báo khi query &gt; 100ms". Mỗi câu SQL EF Core thực thi lâu hơn
/// <see cref="Threshold"/> ghi một log Warning kèm thời gian và câu SQL.
///
/// Chỉ log câu SQL có placeholder (<c>@p0</c>…), KHÔNG log giá trị tham số — tham số có thể là email,
/// password hash, refresh token hash. CorrelationId / UserId / RequestPath có sẵn từ LogContext (CONS-010).
///
/// Không giữ state → dùng chung một instance cho mọi DbContext (giống AuditInterceptor).
/// </summary>
public sealed class SlowQueryInterceptor(ILogger<SlowQueryInterceptor> logger) : DbCommandInterceptor
{
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(100);

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Check(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Check(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Check(command, eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return ValueTask.FromResult(result);
    }

    private void Check(DbCommand command, CommandExecutedEventData eventData)
    {
        if (eventData.Duration <= Threshold)
        {
            return;
        }

        logger.LogWarning(
            "Query CHẬM: {ElapsedMs}ms (ngưỡng {ThresholdMs}ms) — {CommandText}",
            Math.Round(eventData.Duration.TotalMilliseconds, 1),
            Threshold.TotalMilliseconds,
            command.CommandText);
    }
}
