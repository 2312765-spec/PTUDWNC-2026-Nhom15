using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.IntegrationTests.Observability.Support;
using FluentAssertions;
using OpenTelemetry.Metrics;
using Serilog.Events;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability;

/// <summary>
/// FR-OBS-003 — distributed tracing + metrics qua OpenTelemetry (D36–D38).
/// Span/metric đọc từ InMemory exporter mà <see cref="TracingApiFactory"/> gắn thêm vào provider
/// của Program.cs; log đọc từ sink Serilog trong DI.
/// </summary>
public sealed class TracingTests(TracingApiFactory factory) : IClassFixture<TracingApiFactory>
{
    [Fact(DisplayName = "FR-OBS-003: request API sinh server span có http.route (template) + status")]
    public async Task ApiRequest_ProducesServerSpanWithRouteAndStatus()
    {
        var path = $"/api/v1/categories/{NewSlug()}";

        var response = await factory.CreateClient().GetAsync(path);

        var span = await factory.WaitForServerSpanAsync(path);
        span.GetTagItem("http.route").Should().Be("/api/v1/categories/{slug}");
        span.GetTagItem("http.response.status_code").Should().Be((int)response.StatusCode);
    }

    [Fact(DisplayName = "FR-OBS-003/D37: request đọc DB sinh span Npgsql cùng TraceId với server span")]
    public async Task DatabaseRequest_ProducesNpgsqlChildSpanInSameTrace()
    {
        var path = $"/api/v1/categories/{NewSlug()}";

        await factory.CreateClient().GetAsync(path);

        var server = await factory.WaitForServerSpanAsync(path);
        factory.Spans.Should().Contain(s =>
            s.Source.Name == "Npgsql" && s.TraceId == server.TraceId,
            "mỗi câu SQL EF Core gửi xuống phải là một span con của request");
    }

    [Fact(DisplayName = "FR-OBS-003: mọi log của request có TraceId trùng server span, vẫn có CorrelationId")]
    public async Task RequestLogs_CarryServerSpanTraceId()
    {
        var path = $"/api/v1/categories/{NewSlug()}";
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        await factory.CreateClient().SendAsync(request);

        var server = await factory.WaitForServerSpanAsync(path);
        var logs = LogsOf(correlationId);
        logs.Should().NotBeEmpty();
        logs.Should().OnlyContain(e => e.TraceId == server.TraceId,
            "log ↔ trace correlation: lọc Seq theo TraceId phải ra đủ log của request");
    }

    [Fact(DisplayName = "FR-OBS-003: client gửi traceparent -> span và log giữ TraceId của client")]
    public async Task IncomingTraceparent_IsPropagatedToSpanAndLogs()
    {
        var path = $"/api/v1/categories/{NewSlug()}";
        var clientTraceId = ActivityTraceId.CreateRandom();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("traceparent", $"00-{clientTraceId}-{ActivitySpanId.CreateRandom()}-01");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        await factory.CreateClient().SendAsync(request);

        var server = await factory.WaitForServerSpanAsync(path);
        server.TraceId.Should().Be(clientTraceId);
        LogsOf(correlationId).Should().NotBeEmpty()
            .And.OnlyContain(e => e.TraceId == clientTraceId);
    }

    [Fact(DisplayName = "FR-OBS-003: /health/* không sinh span (probe gây nhiễu)")]
    public async Task HealthProbe_IsNotTraced()
    {
        var client = factory.CreateClient();
        var controlPath = $"/api/v1/categories/{NewSlug()}";

        await client.GetAsync("/health/live");
        await client.GetAsync(controlPath);

        // Request đối chứng có span → tracing đang chạy; lúc đó /health/live vẫn không được có span.
        await factory.WaitForServerSpanAsync(controlPath);
        factory.Spans.Should().NotContain(s => (s.GetTagItem("url.path") as string) == "/health/live");
    }

    [Fact(DisplayName = "FR-OBS-003/D38: 500 ghi http.server.request.duration có error.type; 404 thì không")]
    public async Task RequestDurationMetric_MarksOnly5xxAsError()
    {
        var client = factory.CreateClient();

        var boom = await client.GetAsync(TracingApiFactory.BoomPath);
        var notFound = await client.GetAsync($"/api/v1/categories/{NewSlug()}");

        boom.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var errors = await factory.WaitForRequestDurationPointsAsync(p => Tag(p, "http.response.status_code") is 500);
        errors.Should().NotBeEmpty("request count/duration/error rate lấy từ histogram này");
        errors.Should().OnlyContain(p => Tag(p, "error.type") != null);

        var clientErrors = await factory.WaitForRequestDurationPointsAsync(p => Tag(p, "http.response.status_code") is 404);
        clientErrors.Should().NotBeEmpty()
            .And.OnlyContain(p => Tag(p, "error.type") == null, "4xx không tính là lỗi server (D38)");
    }

    [Fact(DisplayName = "FR-OBS-003: span không chứa secret trong body request (mật khẩu đăng nhập)")]
    public async Task Spans_DoNotLeakRequestBodySecrets()
    {
        const string path = "/api/v1/auth/login";
        var secret = $"Secret-{Guid.NewGuid():N}";

        await factory.CreateClient().PostAsJsonAsync(path, new { email = "trace@test.local", password = secret });

        var server = await factory.WaitForServerSpanAsync(path);
        var trace = factory.Spans.Where(s => s.TraceId == server.TraceId).ToList();
        trace.Should().NotContain(s =>
            s.DisplayName.Contains(secret)
            || s.TagObjects.Any(t => t.Value != null && t.Value.ToString()!.Contains(secret)));
    }

    private static string NewSlug() => $"khong-ton-tai-{Guid.NewGuid():N}";

    private List<LogEvent> LogsOf(string correlationId) =>
        factory.Logs.Where(e =>
                e.Properties.TryGetValue(CorrelationIdMiddleware.ItemKey, out var id)
                && (id as ScalarValue)?.Value as string == correlationId)
            .ToList();

    private static object? Tag(MetricPoint point, string key)
    {
        foreach (var tag in point.Tags)
        {
            if (tag.Key == key)
            {
                return tag.Value;
            }
        }

        return null;
    }
}
