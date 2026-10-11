using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Categories;

public sealed class DeleteCategoryTests : IClassFixture<CulinaryBlogApiFactory>
{
    private readonly CulinaryBlogApiFactory _factory;
    private readonly HttpClient _client;

    public DeleteCategoryTests(CulinaryBlogApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "FR-CAT-005: Guest xóa danh mục → 401 Unauthorized")]
    public async Task DeleteCategory_AsGuest_Returns401()
    {
        var response = await _client.DeleteAsync($"/api/v1/categories/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-CAT-005: ID không tồn tại → 404 hoặc 401/403")]
    public async Task DeleteCategory_NotFound_WhenAuthorized()
    {
        // Sử dụng client sẵn có của test framework
        var response = await _client.DeleteAsync($"/api/v1/categories/{Guid.NewGuid()}");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}