using System.Net;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Security;

/// <summary>
/// NFR-SEC-004 — SQL injection: EF Core tham số hóa mọi giá trị; từ khóa tìm kiếm còn được tách theo
/// chữ/số trước khi thành tsquery (D49), nên payload SQL hay toán tử tsquery không gây lỗi 500.
/// Postgres thật (Testcontainers) — lỗi cú pháp SQL/tsquery chỉ lộ ra trên DB thật.
/// </summary>
public sealed class SqlInjectionTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory(DisplayName = "NFR-SEC-004: payload SQL/tsquery trong ô tìm kiếm → 200, không 500")]
    [InlineData("' OR 1=1 --")]
    [InlineData("'; DROP TABLE \"Recipes\"; --")]
    [InlineData("phở' UNION SELECT \"PasswordHash\" FROM \"AspNetUsers\" --")]
    [InlineData("a & | ! :* ( ) <-> \\")]
    public async Task Search_InjectionPayload_Returns200(string payload)
    {
        var response = await _client.GetAsync($"/api/v1/recipes/search?q={Uri.EscapeDataString(payload)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("PasswordHash");
    }

    [Fact(DisplayName = "NFR-SEC-004: sau payload DROP TABLE, bảng Recipes vẫn còn (danh sách công thức 200)")]
    public async Task Search_DropTablePayload_DoesNotDropTable()
    {
        await _client.GetAsync($"/api/v1/recipes/search?q={Uri.EscapeDataString("'; DROP TABLE \"Recipes\"; --")}");

        var response = await _client.GetAsync("/api/v1/recipes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "NFR-SEC-004: payload SQL trong slug → 404, không 500")]
    public async Task GetBySlug_InjectionPayload_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/recipes/{Uri.EscapeDataString("x' OR '1'='1")}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
