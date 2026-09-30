using System.Collections.Concurrent;
using System.Net.Http.Headers;
using CulinaryBlog.API.Logging;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability;

/// <summary>
/// FR-OBS-002/CONS-010 — nội dung của log (không chỉ header X-Correlation-ID như LoggingTests).
/// Gắn một <see cref="ILogEventSink"/> vào DI: Program.cs dùng <c>ReadFrom.Services</c> nên sink
/// này nhận đúng các log event mà Console/File/Seq nhận.
/// </summary>
public sealed class StructuredLoggingTests(CapturingLogApiFactory factory) : IClassFixture<CapturingLogApiFactory>
{
    private const string RequestLogTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    [Fact(DisplayName = "FR-OBS-002: request đã xác thực -> request log có UserId")]
    public async Task AuthenticatedRequest_RequestLogHasUserId()
    {
        var userId = Guid.NewGuid().ToString();
        var jwt = factory.Services.GetRequiredService<IJwtService>();
        var (token, _) = jwt.GenerateAccessToken(userId, "log@test.local", ["Author"]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var correlationId = Guid.NewGuid().ToString();
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        await factory.CreateClient().SendAsync(request);

        var log = await factory.WaitForRequestLogAsync(correlationId);
        ScalarValue(log, "UserId").Should().Be(userId);
    }

    [Fact(DisplayName = "FR-OBS-002: request ẩn danh -> request log không có UserId")]
    public async Task AnonymousRequest_RequestLogHasNoUserId()
    {
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        await factory.CreateClient().SendAsync(request);

        var log = await factory.WaitForRequestLogAsync(correlationId);
        log.Properties.Should().NotContainKey("UserId");
    }

    [Fact(DisplayName = "FR-OBS-002: request log có method, path, status, elapsed, CorrelationId")]
    public async Task RequestLog_HasRequiredProperties()
    {
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        await factory.CreateClient().SendAsync(request);

        var log = await factory.WaitForRequestLogAsync(correlationId);
        log.MessageTemplate.Text.Should().Be(RequestLogTemplate);
        ScalarValue(log, "RequestMethod").Should().Be("GET");
        ScalarValue(log, "RequestPath").Should().Be("/health/live");
        ScalarValue(log, "StatusCode").Should().Be(200);
        log.Properties.Should().ContainKey("Elapsed");
    }

    [Theory(DisplayName = "FR-OBS-002: mức log của request HTTP theo status/thời gian/exception")]
    [InlineData(200, 120.0, false, LogEventLevel.Information)]
    [InlineData(200, 500.0, false, LogEventLevel.Information)]
    [InlineData(200, 500.1, false, LogEventLevel.Warning)]
    [InlineData(404, 900.0, false, LogEventLevel.Warning)]
    [InlineData(500, 10.0, false, LogEventLevel.Error)]
    [InlineData(200, 10.0, true, LogEventLevel.Error)]
    public void RequestLogLevel_FollowsStatusElapsedAndException(int status, double elapsedMs, bool hasException, LogEventLevel expected)
    {
        var context = new DefaultHttpContext();
        context.Response.StatusCode = status;

        RequestLogLevel.Get(context, elapsedMs, hasException ? new InvalidOperationException() : null)
            .Should().Be(expected);
    }

    [Fact(DisplayName = "FR-OBS-002: sink Console xuất JSON, sink File rolling theo ngày")]
    public void SerilogConfig_HasJsonConsoleAndDailyRollingFile()
    {
        var config = factory.Services.GetRequiredService<IConfiguration>().GetSection("Serilog:WriteTo");

        config["Console:Args:formatter"].Should().Contain("CompactJsonFormatter");
        config["File:Name"].Should().Be("File");
        config["File:Args:rollingInterval"].Should().Be("Day");
        config["File:Args:formatter"].Should().Contain("CompactJsonFormatter");
    }

    private static object? ScalarValue(LogEvent log, string property) =>
        log.Properties.TryGetValue(property, out var value) ? (value as ScalarValue)?.Value : null;
}

/// <summary>API trong bộ nhớ, kèm sink giữ lại mọi log event để test đọc.</summary>
public sealed class CapturingLogApiFactory : CulinaryBlogApiFactory
{
    private readonly CapturingSink _sink = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(_sink));
    }

    /// <summary>
    /// RequestLoggingMiddleware ghi log sau khi pipeline chạy xong, có thể muộn hơn lúc client
    /// nhận response — nên chờ ngắn thay vì đọc ngay.
    /// </summary>
    public async Task<LogEvent> WaitForRequestLogAsync(string correlationId)
    {
        for (var i = 0; i < 50; i++)
        {
            var log = _sink.Events.FirstOrDefault(e =>
                e.Properties.ContainsKey("StatusCode")
                && e.Properties.TryGetValue("CorrelationId", out var id)
                && (id as ScalarValue)?.Value as string == correlationId);
            if (log is not null)
            {
                return log;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"Không thấy request log cho CorrelationId {correlationId}.");
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
