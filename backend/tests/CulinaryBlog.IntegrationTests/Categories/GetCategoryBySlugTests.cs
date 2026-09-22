using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Categories;

/// <summary>
/// FR-CAT-002 — SRS mục 3.2/8.2 + docs/decisions.md D8.
///
/// Regression test cho bug phát hiện lúc review PR (2026-09-23): CategoryRepository.GetBySlugAsync
/// thiếu .Include(c => c.Recipes) khiến trang chi tiết danh mục luôn trả danh sách công thức
/// rỗng dù danh mục có công thức thật. Test Recipes_IncludesPublishedRecipe_InSameCategory là
/// test lẽ ra phải fail trước khi vá, và phải pass sau khi vá.
/// </summary>
public sealed class GetCategoryBySlugTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-CAT-002: slug không tồn tại → 404 CATEGORY_NOT_FOUND")]
    public async Task GetBySlug_UnknownSlug_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/categories/khong-ton-tai-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.CategoryNotFound);
    }

    [Fact(DisplayName = "FR-CAT-002 (regression): danh mục có công thức Published thật → phải xuất hiện trong danh sách")]
    public async Task Recipes_IncludesPublishedRecipe_InSameCategory()
    {
        var (categorySlug, recipeId) = await SeedCategoryWithPublishedRecipeAsync();

        var response = await _client.GetAsync($"/api/v1/categories/{categorySlug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CategoryDetailResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Recipes.Items.Should().ContainSingle(r => r.Id == recipeId);
        body.Recipes.TotalCount.Should().Be(1);
        body.Category.RecipeCount.Should().Be(1);
    }

    [Fact(DisplayName = "FR-CAT-002: Guest không thấy Draft của người khác")]
    public async Task Recipes_Guest_DoesNotSeeOtherAuthorsDraft()
    {
        var (categorySlug, _) = await SeedCategoryWithDraftRecipeAsync(authorId: "someone-else");

        var response = await _client.GetAsync($"/api/v1/categories/{categorySlug}");

        var body = await response.Content.ReadFromJsonAsync<CategoryDetailResponseDto>(JsonOptions);
        body!.Recipes.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-CAT-002: chủ sở hữu đăng nhập thấy được Draft của chính mình")]
    public async Task Recipes_Owner_SeesOwnDraft()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var (categorySlug, recipeId) = await SeedCategoryWithDraftRecipeAsync(authorId: userId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync($"/api/v1/categories/{categorySlug}");
        _client.DefaultRequestHeaders.Authorization = null;

        var body = await response.Content.ReadFromJsonAsync<CategoryDetailResponseDto>(JsonOptions);
        body!.Recipes.Items.Should().ContainSingle(r => r.Id == recipeId);
    }

    private async Task<(string Token, string UserId)> RegisterAuthorAsync()
    {
        var email = $"cat-owner-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "Str0ng!Pass1", displayName = "Tác giả test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return (body!.AccessToken, body.User.Id);
    }

    private async Task<(string Slug, Guid RecipeId)> SeedCategoryWithPublishedRecipeAsync() =>
        await SeedCategoryWithRecipeAsync("author-published", RecipeStatus.Published);

    private async Task<(string Slug, Guid RecipeId)> SeedCategoryWithDraftRecipeAsync(string authorId) =>
        await SeedCategoryWithRecipeAsync(authorId, RecipeStatus.Draft);

    private async Task<(string Slug, Guid RecipeId)> SeedCategoryWithRecipeAsync(string authorId, RecipeStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var slug = $"danh-muc-{Guid.NewGuid():N}"[..20];
        var category = Category.Create("Danh mục test", slug, "desc", null, 0);
        db.Categories.Add(category);

        var recipe = Recipe.Create(
            "Công thức test", $"recipe-{Guid.NewGuid():N}", "mô tả", 10, 30, 2,
            RecipeDifficulty.Easy, category.Id, authorId, status: status);
        db.Recipes.Add(recipe);

        await db.SaveChangesAsync();

        return (slug, recipe.Id);
    }
}
