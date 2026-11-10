using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability;

/// <summary>
/// NFR-PERF-004 — "Slow query log: cảnh báo khi query > 100ms" và "mọi cột ORDER BY có B-tree index".
/// </summary>
public sealed class SlowQueryTests(SqlCapturingApiFactory factory) : IClassFixture<SqlCapturingApiFactory>
{
    private const string InterceptorContext = "CulinaryBlog.Infrastructure.Persistence.SlowQueryInterceptor";

    [Fact(DisplayName = "NFR-PERF-004: câu SQL > 100 ms → log Warning kèm thời gian và câu SQL")]
    public async Task SlowCommand_LogsWarning()
    {
        // Chỉ test này dùng pg_sleep — câu SQL hằng (analyzer EF1002 cấm nối chuỗi vào SQL).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            await db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM pg_sleep(0.15)").ToListAsync();
        }

        var warning = SlowQueryWarnings().Should().ContainSingle(e => RenderedSql(e).Contains("pg_sleep")).Subject;
        warning.Level.Should().Be(LogEventLevel.Warning);
        ((double)((ScalarValue)warning.Properties["ElapsedMs"]).Value!).Should().BeGreaterThan(100);
    }

    [Fact(DisplayName = "NFR-PERF-004: câu SQL nhanh → không log Warning")]
    public async Task FastCommand_NoWarning()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            await db.Database.SqlQuery<int>($"SELECT 424242 AS \"Value\"").ToListAsync();
        }

        SlowQueryWarnings().Should().NotContain(e => RenderedSql(e).Contains("424242"));
    }

    [Fact(DisplayName = "NFR-PERF-004/FR-RCP-001: ORDER BY CreatedAt DESC, Id có index (Status, CreatedAt DESC, Id)")]
    public async Task Recipes_HasStatusCreatedAtIndex()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var definition = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'IX_Recipes_Status_CreatedAt'")
            .SingleOrDefaultAsync();

        definition.Should().NotBeNull("thiếu index cho ORDER BY CreatedAt của GET /recipes");
        definition.Should().Contain("(\"Status\", \"CreatedAt\" DESC, \"Id\")");
    }

    private IEnumerable<LogEvent> SlowQueryWarnings() =>
        factory.Events.Where(e => e.Properties.TryGetValue("SourceContext", out var src)
                                  && (src as ScalarValue)?.Value as string == InterceptorContext);

    private static string RenderedSql(LogEvent e) =>
        e.Properties.TryGetValue("CommandText", out var sql) ? (sql as ScalarValue)?.Value as string ?? "" : "";
}
