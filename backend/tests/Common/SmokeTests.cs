using System.Net;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

public class SmokeTests : IClassFixture<CulinaryBlogApiFactory>
{
    private readonly HttpClient _client;

    public SmokeTests(CulinaryBlogApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Health Check /health trả về 200 OK")]
    public async Task HealthCheck_Returns200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "Các endpoint chưa hiện thực trả về 501 Not Implemented")]
    [InlineData("/api/v1/recipes")]
    [InlineData("/api/v1/recipes/search?q=test")]
    [InlineData("/api/v1/recipes/mon-an-mau")]
    // LƯU Ý QUAN TRỌNG (Sổ tay Người B - Chương 7.4):
    // Đã gỡ bỏ "/api/v1/categories" khỏi danh sách 501 vì FR-CAT-001 đã được hiện thực hoàn chỉnh (trả 200 OK)
    public async Task UnimplementedEndpoints_Return501(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact(DisplayName = "FR-CAT-001: /api/v1/categories không còn trả 501 mà trả 200 OK")]
    public async Task CategoriesEndpoint_IsImplemented_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
