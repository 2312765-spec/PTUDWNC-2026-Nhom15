using System.Net;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// Smoke test — chứng minh khung API khởi động được và pipeline hoạt động.
/// Không cần PostgreSQL hay Redis đang chạy.
/// </summary>
public class SmokeTests(CulinaryBlogApiFactory factory) : IClassFixture<CulinaryBlogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-OBS-001: /health/live luôn trả 200 khi process còn sống")]
    public async Task HealthLive_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CONS-010: response luôn có header X-Correlation-ID")]
    public async Task EveryResponse_HasCorrelationIdHeader()
    {
        var response = await _client.GetAsync("/health/live");

        response.Headers.Should().ContainKey("X-Correlation-ID");
    }

    [Fact(DisplayName = "CONS-010: CorrelationId do client gửi được giữ nguyên")]
    public async Task ClientCorrelationId_IsEchoedBack()
    {
        const string correlationId = "test-correlation-id-123";
        _client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        var response = await _client.GetAsync("/health/live");

        response.Headers.GetValues("X-Correlation-ID").Should().Contain(correlationId);
    }

    [Theory(DisplayName = "Endpoint chưa hiện thực trả 501 kèm mã FR")]
    //[InlineData("/api/v1/categories")]
    [InlineData("/api/v1/recipes")]
    public async Task PendingEndpoints_Return501(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact(DisplayName = "NFR-SEC-006: endpoint cần quyền trả 401 khi chưa đăng nhập")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
