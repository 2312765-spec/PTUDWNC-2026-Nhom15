using System.Net;
using CulinaryBlog.IntegrationTests.Observability.Support;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability;

/// <summary>FR-OBS-001 — 3 endpoint health check, mỗi endpoint một mục đích khác nhau.</summary>
public sealed class HealthTests(HealthyApiFactory factory) : IClassFixture<HealthyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-OBS-001: GET /health khi DB + Redis + MinIO đều khỏe -> 200 Healthy")]
    public async Task Health_AllComponentsUp_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact(DisplayName = "FR-OBS-001: GET /health/live luôn Healthy, không phụ thuộc DB/Redis/MinIO")]
    public async Task Live_AlwaysHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact(DisplayName = "FR-OBS-001: GET /health/ready khi DB + Redis khỏe -> 200 Healthy")]
    public async Task Ready_DatabaseAndRedisUp_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}

/// <summary>Ca lỗi của FR-OBS-001: readiness phải fail thật khi database không kết nối được.</summary>
public sealed class HealthReadyDegradedTests(DegradedApiFactory factory) : IClassFixture<DegradedApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-OBS-001: GET /health/ready khi database không kết nối được -> 503 Unhealthy")]
    public async Task Ready_DatabaseDown_ReturnsUnhealthy()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Be("Unhealthy");
    }
}
