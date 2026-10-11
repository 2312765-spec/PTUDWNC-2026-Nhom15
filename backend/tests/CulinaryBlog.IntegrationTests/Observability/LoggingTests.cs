using System.Net;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability;

/// <summary>
/// FR-OBS-002/CONS-010 — CorrelationIdMiddleware. Dùng <c>CulinaryBlogApiFactory</c> (không cần
/// DB) và gọi "/health/live" — endpoint duy nhất chắc chắn 200 mà không cần hạ tầng thật, vì phần
/// cần kiểm tra ở đây là middleware tầng HTTP, không phải nghiệp vụ của endpoint.
/// </summary>
public sealed class LoggingTests : IClassFixture<CulinaryBlogApiFactory>
{
    private readonly HttpClient _client;

    public LoggingTests(CulinaryBlogApiFactory factory) => _client = factory.CreateClient();

    [Fact(DisplayName = "FR-OBS-002: không gửi X-Correlation-ID -> response tự sinh một giá trị")]
    public async Task Request_WithoutCorrelationId_ServerGeneratesOne()
    {
        var response = await _client.GetAsync("/health/live");

        response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values).Should().BeTrue();
        var correlationId = values!.Single();
        correlationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    [Fact(DisplayName = "FR-OBS-002: gửi X-Correlation-ID -> server giữ nguyên, không tự sinh mới")]
    public async Task Request_WithCorrelationId_ServerEchoesSameValue()
    {
        var sentId = "client-side-id-12345";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, sentId);

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values).Should().BeTrue();
        values!.Single().Should().Be(sentId);
    }

    [Fact(DisplayName = "FR-OBS-002: route không tồn tại (404) vẫn có X-Correlation-ID trong response")]
    public async Task Request_UnknownRoute_StillHasCorrelationId()
    {
        var response = await _client.GetAsync("/khong-ton-tai");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out _).Should().BeTrue();
    }
}
