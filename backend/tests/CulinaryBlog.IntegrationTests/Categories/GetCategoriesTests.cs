using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Categories;

/// <summary>
/// FR-CAT-001 — GET /api/v1/categories. Dùng PostgresApiFactory (Testcontainers) vì handler
/// đọc DB thật qua ICategoryRepository. Trước đây test này nằm trong SmokeTests.cs (kỳ vọng
/// 501 lúc endpoint còn là stub) rồi bị comment ra khi endpoint làm xong thay vì sửa — bug
/// thiếu đăng ký DI (ICategoryRepository) vì vậy không bị bắt. Tách thành test riêng ở đây.
/// </summary>
public sealed class GetCategoriesTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-CAT-001: chưa có category nào → 200 kèm mảng rỗng")]
    public async Task GetCategories_Empty_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/v1/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<CategoryDto>>();
        body.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName = "FR-CAT-001: Guest xem được danh sách category, không cần đăng nhập")]
    public async Task GetCategories_NoAuth_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/categories");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }
}
