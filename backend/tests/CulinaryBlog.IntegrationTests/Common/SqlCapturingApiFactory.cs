using System.Collections.Concurrent;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Testcontainers.PostgreSql;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// NFR-PERF-004 — API với PostgreSQL thật, giữ lại MỌI log event (kể cả câu SQL EF Core thực thi)
/// để test đọc được câu SQL thật của từng request.
///
/// Redis trỏ vào cổng đóng: request luôn chạy xuống DB (D90 — FailFast, không chờ), nên đọc lại cùng
/// một URL lần hai vẫn sinh SQL, không bị cache che.
/// </summary>
public sealed class SqlCapturingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string EfCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("culinaryblog_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly CapturingSink _sink = new();

    public IReadOnlyCollection<LogEvent> Events => _sink.Events;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        await db.Database.MigrateAsync();
        await IdentityRoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync().AsTask();
        await base.DisposeAsync().AsTask();
    }

    /// <summary>
    /// Câu SQL EF Core đã thực thi trong request mang <paramref name="correlationId"/>
    /// (header <c>X-Correlation-ID</c> — CorrelationIdMiddleware đẩy vào LogContext).
    /// </summary>
    public IReadOnlyList<string> SqlForRequest(string correlationId) =>
        _sink.Events
            .Where(e => e.Properties.TryGetValue("SourceContext", out var src)
                        && (src as ScalarValue)?.Value as string == EfCommandCategory
                        && e.Level == LogEventLevel.Information
                        && e.Properties.TryGetValue("CorrelationId", out var id)
                        && (id as ScalarValue)?.Value as string == correlationId
                        && e.Properties.ContainsKey("commandText"))
            .Select(e => (string)((ScalarValue)e.Properties["commandText"]).Value!)
            .ToList();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", "localhost:1");
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:Port", "1");
        // appsettings.json để category này ở Warning — hạ xuống để thấy "Executed DbCommand".
        builder.UseSetting($"Serilog:MinimumLevel:Override:{EfCommandCategory}", "Information");

        builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(_sink));
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
