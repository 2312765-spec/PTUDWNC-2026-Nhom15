using CulinaryBlog.Application.Common.Observability;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CulinaryBlog.API.Extensions;

/// <summary>
/// FR-OBS-003 — distributed tracing + metrics qua OpenTelemetry.
/// <list type="bullet">
/// <item>Trace: ASP.NET Core (bỏ <c>/health*</c>), HttpClient, Npgsql (D37).</item>
/// <item>Metric: histogram <c>http.server.request.duration</c> của ASP.NET Core = request count +
/// duration + error rate (D38), HttpClient, và <see cref="RecipeMetrics"/> (D39).</item>
/// <item>Export OTLP theo từng signal, chỉ bật khi có endpoint cấu hình (D36).</item>
/// </list>
/// TraceId vào log không cần code ở đây: Serilog tự lấy <c>Activity.Current</c> cho mỗi log event.
/// </summary>
public static class TelemetryExtensions
{
    public const string ServiceName = "culinary-blog-api";

    public static IServiceCollection AddAppTelemetry(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // D36: Testing không export ra ngoài (test gắn InMemory exporter riêng).
        var canExport = !environment.IsEnvironment("Testing");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                ServiceName,
                serviceVersion: typeof(TelemetryExtensions).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddNpgsql();

                if (canExport && HasOtlpEndpoint(configuration, "TRACES"))
                {
                    tracing.AddOtlpExporter(options => ApplySignalOverrides(options, configuration, "TRACES"));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(RecipeMetrics.MeterName);

                if (canExport && HasOtlpEndpoint(configuration, "METRICS"))
                {
                    metrics.AddOtlpExporter(options => ApplySignalOverrides(options, configuration, "METRICS"));
                }
            });

        return services;
    }

    /// <summary>
    /// Biến chuẩn của OpenTelemetry — exporter tự đọc chúng từ IConfiguration (env var hoặc appsettings).
    /// Dev chỉ đặt <c>OTEL_EXPORTER_OTLP_TRACES_ENDPOINT</c> (Seq) nên metric không export (D36);
    /// prod đặt <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (Collector) nên cả hai cùng đi.
    /// </summary>
    private static bool HasOtlpEndpoint(IConfiguration configuration, string signal) =>
        !string.IsNullOrWhiteSpace(configuration[$"OTEL_EXPORTER_OTLP_{signal}_ENDPOINT"])
        || !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    /// <summary>
    /// <c>AddOtlpExporter()</c> chỉ tự đọc biến chung <c>OTEL_EXPORTER_OTLP_*</c>; biến riêng từng signal
    /// bị bỏ qua và exporter rơi về gRPC <c>localhost:4317</c> — không gửi được tới Seq (kiểm tay
    /// FR-OBS-003, 2026-09-30). Áp tay: endpoint riêng là URL đầy đủ (vd. <c>.../v1/traces</c>), dùng nguyên.
    /// </summary>
    private static void ApplySignalOverrides(OtlpExporterOptions options, IConfiguration configuration, string signal)
    {
        if (Uri.TryCreate(configuration[$"OTEL_EXPORTER_OTLP_{signal}_ENDPOINT"], UriKind.Absolute, out var endpoint))
        {
            options.Endpoint = endpoint;
        }

        var protocol = configuration[$"OTEL_EXPORTER_OTLP_{signal}_PROTOCOL"] ?? configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];
        if (protocol is not null)
        {
            options.Protocol = protocol == "http/protobuf" ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
        }
    }
}
