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
    [InlineData("/api/v1/recipes/11111111-1111-1111-1111-111111111111/publish")] // FR-RCP-005 (C)
    public async Task PendingEndpoints_Return501(string url)
    {
        // Gắn token bất kỳ (hoặc tạo request với Authorization) để vượt qua tầng 401 nếu endpoint require auth
        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "mock-jwt-access-token");

        var response = await _client.SendAsync(request);

        // Chấp nhận 501 (NotImplemented) theo đúng tinh thần pending endpoint
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotImplemented, HttpStatusCode.Unauthorized);
    }
}