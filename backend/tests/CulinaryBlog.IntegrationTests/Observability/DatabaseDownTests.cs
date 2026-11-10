using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.IntegrationTests.Observability.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability;

/// <summary>
/// NFR-MAINT-002 — error case cho các endpoint không có input / không cần đăng nhập: lỗi duy nhất có thể
/// xảy ra là phía server. DB chết thật (cổng 1 đóng — <see cref="DegradedApiFactory"/>), không giả lập.
/// </summary>
public sealed class DatabaseDownTests(DegradedApiFactory factory) : IClassFixture<DegradedApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-CAT-001/NFR-REL-002/CONS-005: DB chết → GET /categories 500 problem+json, không lộ stack trace")]
    public async Task GetCategories_DatabaseDown_Returns500ProblemWithoutStackTrace()
    {
        var response = await _client.GetAsync("/api/v1/categories");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var raw = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        problem!.Type.Should().Be("INTERNAL_SERVER_ERROR");
        problem.Status.Should().Be(500);
        raw.Should().NotContainAny(["   at ", "Npgsql", "Exception"], "không lộ stack trace / chi tiết hạ tầng ra client");
    }

    [Fact(DisplayName = "FR-OBS-001: DB chết → GET /health 503 Unhealthy")]
    public async Task Health_DatabaseDown_Returns503()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Unhealthy");
    }
}
