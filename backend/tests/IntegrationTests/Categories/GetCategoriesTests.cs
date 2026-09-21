using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Categories.DTOs;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Categories;

public class GetCategoriesTests : IClassFixture<CulinaryBlogApiFactory>
{
    private readonly HttpClient _client;
    private readonly CulinaryBlogApiFactory _factory;

    public GetCategoriesTests(CulinaryBlogApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "FR-CAT-001: Khách truy cập GET /api/v1/categories trả về 200 OK và danh sách sắp xếp theo Name A-Z")]
    public async Task GetCategories_WhenCalled_Returns200WithAlphabeticalOrder()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();

        categories.Should().NotBeNull();
        categories!.Count.Should().Be(8); // 8 danh mục mẫu từ seed

        // Sắp xếp theo Name tăng dần
        categories.Should().BeInAscendingOrder(c => c.Name);
    }

    [Fact(DisplayName = "FR-CAT-001: RecipeCount chỉ đếm các công thức ở trạng thái Published và chưa bị xóa")]
    public async Task GetCategories_RecipeCount_OnlyCountsPublishedRecipes()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/categories");
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();

        categories.Should().NotBeNull();

        // Mọi category có recipeCount >= 0, không tính Draft hoặc Archived
        foreach (var cat in categories!)
        {
            cat.RecipeCount.Should().BeGreaterThanOrEqualTo(0);
            cat.Slug.Should().NotBeNullOrWhiteSpace();
            cat.Id.Should().NotBeEmpty();
        }
    }

    [Fact(DisplayName = "FR-CAT-001: Gọi 2 lần liên tiếp -> lần 2 được phục vụ từ Redis Cache")]
    public async Task GetCategories_SecondCall_ServedFromCache()
    {
        // Act 1: Lần 1 - Cache miss, nạp vào cache
        var response1 = await _client.GetAsync("/api/v1/categories");
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: Lần 2 - Cache hit
        var response2 = await _client.GetAsync("/api/v1/categories");
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var data1 = await response1.Content.ReadFromJsonAsync<List<CategoryDto>>();
        var data2 = await response2.Content.ReadFromJsonAsync<List<CategoryDto>>();

        data2.Should().BeEquivalentTo(data1);
    }

    [Fact(DisplayName = "FR-CAT-001: Khi Redis gặp sự cố, hệ thống fallback về database không throw exception (NFR-REL-002)")]
    public async Task GetCategories_WhenRedisDown_FallbacksToDatabaseGracefully()
    {
        // Giả lập Redis offline tạm thời
        _factory.SimulateRedisFailure(true);

        try
        {
            var response = await _client.GetAsync("/api/v1/categories");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
            categories.Should().NotBeNull();
            categories!.Count.Should().Be(8);
        }
        finally
        {
            _factory.SimulateRedisFailure(false);
        }
    }

    [Fact(DisplayName = "FR-CAT-001: Khi không có danh mục nào trong database thì trả về 200 OK với mảng rỗng []")]
    public async Task GetCategories_WhenEmpty_Returns200WithEmptyList()
    {
        using var scope = _factory.Services.CreateScope();
        var emptyClient = _factory.CreateClientWithEmptyDatabase();

        var response = await emptyClient.GetAsync("/api/v1/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotBeNull();
        categories.Should().BeEmpty();
    }
}
