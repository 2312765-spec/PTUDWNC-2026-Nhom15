using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog.Core;
using Serilog.Events;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability.Support;

/// <summary>
/// FR-OBS-003 — API với Postgres + Redis thật (Testcontainers), kèm:
/// <list type="bullet">
/// <item>InMemory exporter cho trace và metric — gắn qua <c>ConfigureOpenTelemetry*Provider</c>, nên chỉ
/// nhận được dữ liệu khi Program.cs thật sự bật OpenTelemetry (không có provider → danh sách rỗng).</item>
/// <item>Sink Serilog giữ mọi log event (như <c>CapturingLogApiFactory</c> của FR-OBS-002).</item>
/// <item>Route <see cref="BoomPath"/> luôn ném exception → 500 qua GlobalExceptionMiddleware (D38).</item>
/// </list>
/// </summary>
public sealed class TracingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BoomPath = "/__test/boom";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("culinaryblog_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().Build();

    private readonly LockedCollection<Activity> _spans = new();
    private readonly LockedCollection<MetricSnapshot> _metrics = new();
    private readonly ConcurrentQueue<LogEvent> _logs = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync().AsTask();
        await _redis.DisposeAsync().AsTask();
        await base.DisposeAsync().AsTask();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting (không phải ConfigureAppConfiguration) — xem lý do ở PostgresApiFactory.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:Port", "1");

        builder.ConfigureTestServices(services =>
        {
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_spans));
            services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddInMemoryExporter(_metrics));
            services.AddSingleton<ILogEventSink>(new QueueSink(_logs));
            services.AddSingleton<IStartupFilter, BoomStartupFilter>();
        });
    }

    public IReadOnlyList<Activity> Spans => _spans.Snapshot();

    public IReadOnlyList<LogEvent> Logs => [.. _logs];

    /// <summary>Span server của ASP.NET Core kết thúc SAU khi client nhận response — chờ ngắn.</summary>
    public async Task<Activity> WaitForServerSpanAsync(string path)
    {
        for (var i = 0; i < 100; i++)
        {
            var span = Spans.FirstOrDefault(s =>
                s.Kind == ActivityKind.Server && (s.GetTagItem("url.path") as string) == path);
            if (span is not null)
            {
                return span;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"Không thấy server span cho {path} — OpenTelemetry tracing chưa bật?");
    }

    /// <summary>
    /// Histogram <c>http.server.request.duration</c> được ghi lúc HostingApplication dispose context,
    /// có thể muộn hơn response — flush lặp tới khi thấy điểm dữ liệu khớp.
    /// </summary>
    public async Task<IReadOnlyList<MetricPoint>> WaitForRequestDurationPointsAsync(Func<MetricPoint, bool> match)
    {
        var provider = Services.GetService<MeterProvider>();
        for (var i = 0; i < 50 && provider is not null; i++)
        {
            _metrics.Clear();
            provider.ForceFlush();
            var points = _metrics.Snapshot()
                .Where(m => m.Name == "http.server.request.duration")
                .SelectMany(m => m.MetricPoints)
                .Where(match)
                .ToList();
            if (points.Count > 0)
            {
                return points;
            }

            await Task.Delay(20);
        }

        return [];
    }

    private sealed class BoomStartupFilter : IStartupFilter
    {
        // Thêm SAU pipeline của Program.cs: không endpoint nào khớp BoomPath nên request rơi xuống đây,
        // exception bay ngược lên GlobalExceptionMiddleware → 500 RFC 7807.
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(BoomPath, boom => boom.Run(_ => throw new InvalidOperationException("FR-OBS-003 test 500")));
        };
    }

    private sealed class QueueSink(ConcurrentQueue<LogEvent> events) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);
    }

    /// <summary>InMemory exporter ghi từ thread export, test đọc từ thread khác — khóa cả hai phía.</summary>
    private sealed class LockedCollection<T> : ICollection<T>
    {
        private readonly List<T> _items = [];

        public int Count { get { lock (_items) { return _items.Count; } } }

        public bool IsReadOnly => false;

        public void Add(T item) { lock (_items) { _items.Add(item); } }

        public void Clear() { lock (_items) { _items.Clear(); } }

        public bool Contains(T item) { lock (_items) { return _items.Contains(item); } }

        public void CopyTo(T[] array, int arrayIndex) { lock (_items) { _items.CopyTo(array, arrayIndex); } }

        public bool Remove(T item) { lock (_items) { return _items.Remove(item); } }

        public List<T> Snapshot() { lock (_items) { return [.. _items]; } }

        public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}