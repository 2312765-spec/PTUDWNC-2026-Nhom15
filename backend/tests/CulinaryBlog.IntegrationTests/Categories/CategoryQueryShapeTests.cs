using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Categories;

/// <summary>
/// NFR-PERF-004 — câu SQL thật của FR-CAT-001 / FR-CAT-002 phải dùng projection và phân trang trong
/// PostgreSQL, không nạp toàn bộ recipe + ảnh vào bộ nhớ (perf-summary 2026-11-03: /categories 532 ms).
/// </summary>
public sealed class CategoryQueryShapeTests(SqlCapturingApiFactory factory) : IClassFixture<SqlCapturingApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "NFR-PERF-004/FR-CAT-001: GET /categories — 1 câu SQL đếm bằng COUNT, không nạp recipe/ảnh")]
    public async Task GetCategories_CountsInSql_SingleQueryWithoutImages()
    {
        var authorId = await RegisterAuthorAsync();
        var category = await SeedCategoryAsync();
        await SeedRecipeAsync(category.Id, authorId, RecipeStatus.Published, withImage: true);
        await SeedRecipeAsync(category.Id, authorId, RecipeStatus.Published, withImage: true);
        await SeedRecipeAsync(category.Id, authorId, RecipeStatus.Draft);
        await SeedRecipeAsync(category.Id, authorId, RecipeStatus.Archived);
        await SeedRecipeAsync(category.Id, authorId, RecipeStatus.Published, deleted: true);

        var (response, sql) = await GetWithSqlAsync("/api/v1/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>(JsonOptions);
        categories!.Single(c => c.Id == category.Id).RecipeCount.Should().Be(2, "chỉ đếm Published chưa xóa");

        sql.Should().ContainSingle("FR-CAT-001 chỉ cần một câu SQL");
        sql[0].Should().NotContain("\"RecipeImages\"", "đếm recipe không cần nạp ảnh");
        sql[0].Should().ContainEquivalentOf("count(", "recipeCount phải đếm trong PostgreSQL");
    }

    [Fact(DisplayName = "NFR-PERF-004/FR-CAT-002: GET /categories/{slug} — phân trang trong SQL, chỉ nạp recipe của trang")]
    public async Task GetCategoryBySlug_PagesInSql()
    {
        var authorId = await RegisterAuthorAsync();
        var category = await SeedCategoryAsync();
        var published = new List<Guid>();
        for (var i = 0; i < 5; i++)
            published.Add(await SeedRecipeAsync(category.Id, authorId, RecipeStatus.Published, withImage: true));

        var (response, sql) = await GetWithSqlAsync($"/api/v1/categories/{category.Slug}?page=2&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CategoryDetailResponseDto>(JsonOptions);
        body!.Recipes.TotalCount.Should().Be(5);
        body.Category.RecipeCount.Should().Be(5);
        // Mới nhất trước: published[4], [3] | [2], [1] | [0]
        body.Recipes.Items.Select(r => r.Id).Should().Equal(published[2], published[1]);
        body.Recipes.Items.Should().OnlyContain(r => r.FeaturedImageUrl != null);

        var recipeQueries = sql.Where(s => s.Contains("FROM \"Recipes\"")).ToList();
        recipeQueries.Should().NotBeEmpty();
        // OFFSET chỉ xuất hiện khi Skip/Take chạy trong SQL. Không dùng "LIMIT": FirstOrDefault trên
        // Categories cũng sinh "LIMIT 1" dù recipe vẫn bị nạp hết qua JOIN.
        recipeQueries.Should().OnlyContain(
            s => s.Contains("OFFSET") || s.Contains("count(", StringComparison.OrdinalIgnoreCase),
            "mọi câu đọc Recipes phải phân trang (LIMIT … OFFSET) hoặc chỉ đếm (COUNT)");
    }

    [Fact(DisplayName = "FR-CAT-002/NFR-PERF-004: quyền xem giữ nguyên — Guest chỉ Published; chủ sở hữu thêm Draft của mình; không ai thấy Archived")]
    public async Task GetCategoryBySlug_VisibilityRulesUnchanged()
    {
        var (ownerToken, ownerId) = await RegisterAuthorWithTokenAsync();
        var otherId = await RegisterAuthorAsync();
        var category = await SeedCategoryAsync();
        var published = await SeedRecipeAsync(category.Id, otherId, RecipeStatus.Published);
        var ownDraft = await SeedRecipeAsync(category.Id, ownerId, RecipeStatus.Draft);
        await SeedRecipeAsync(category.Id, otherId, RecipeStatus.Draft);
        await SeedRecipeAsync(category.Id, ownerId, RecipeStatus.Archived);
        await SeedRecipeAsync(category.Id, ownerId, RecipeStatus.Published, deleted: true);

        var guest = await _client.GetFromJsonAsync<CategoryDetailResponseDto>($"/api/v1/categories/{category.Slug}", JsonOptions);
        guest!.Recipes.Items.Select(r => r.Id).Should().BeEquivalentTo([published]);
        guest.Recipes.TotalCount.Should().Be(1);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/categories/{category.Slug}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var ownerResponse = await _client.SendAsync(request);
        var owner = await ownerResponse.Content.ReadFromJsonAsync<CategoryDetailResponseDto>(JsonOptions);
        owner!.Recipes.Items.Select(r => r.Id).Should().BeEquivalentTo([published, ownDraft]);
        owner.Recipes.TotalCount.Should().Be(2);
        owner.Category.RecipeCount.Should().Be(2);
    }

    private async Task<(HttpResponseMessage Response, IReadOnlyList<string> Sql)> GetWithSqlAsync(string url)
    {
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Correlation-ID", correlationId);
        var response = await _client.SendAsync(request);
        return (response, factory.SqlForRequest(correlationId));
    }

    private async Task<string> RegisterAuthorAsync() => (await RegisterAuthorWithTokenAsync()).UserId;

    private async Task<(string Token, string UserId)> RegisterAuthorWithTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"perf-{Guid.NewGuid():N}@example.com", password = "Str0ng!Pass1", displayName = "Tác giả perf" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return (body!.AccessToken, body.User.Id);
    }

    private async Task<Category> SeedCategoryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var unique = Guid.NewGuid().ToString("N")[..12];
        var category = Category.Create($"Danh mục perf {unique}", $"danh-muc-perf-{unique}", "desc", null, 0);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    /// <summary>Mỗi recipe một lần SaveChanges → CreatedAt khác nhau, thứ tự trang xác định được.</summary>
    private async Task<Guid> SeedRecipeAsync(
        Guid categoryId, string authorId, RecipeStatus status, bool withImage = false, bool deleted = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var recipe = Recipe.Create(
            "Công thức perf", $"recipe-perf-{Guid.NewGuid():N}", "mô tả", 10, 30, 2,
            RecipeDifficulty.Easy, categoryId, authorId, status: status);
        if (withImage)
            recipe.AttachImage($"https://cdn.test/{Guid.NewGuid():N}.jpg", isPrimary: true);
        if (deleted)
            recipe.SoftDelete();
        db.Recipes.Add(recipe);

        await db.SaveChangesAsync();
        await Task.Delay(5);
        return recipe.Id;
    }
}
