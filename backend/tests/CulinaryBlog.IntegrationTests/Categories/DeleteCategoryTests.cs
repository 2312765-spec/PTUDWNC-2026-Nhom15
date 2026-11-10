using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Categories;

/// <summary>
/// FR-CAT-005 — DELETE /api/v1/categories/{id} trên PostgreSQL thật (D2: soft delete, còn recipe chưa xóa → 409).
///
/// Viết lại 2026-11-10 (NFR-MAINT-002): bản cũ chạy trên factory không có DB và không gửi token — test
/// "ID không tồn tại" luôn nhận 401 nhưng assert chấp nhận "404 hoặc 401/403", nên không kiểm được gì;
/// không có happy path 204 và không có case 409.
/// </summary>
public sealed class DeleteCategoryTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-CAT-005/D2: Admin xóa danh mục rỗng → 204, soft delete (dòng còn trong DB, IsDeleted = true), biến khỏi GET /categories")]
    public async Task Delete_EmptyCategory_Returns204AndSoftDeletes()
    {
        var admin = await RegisterAdminAsync();
        var category = await SeedCategoryAsync();
        (await _client.GetAsync("/api/v1/categories")).EnsureSuccessStatusCode(); // nạp cache — xóa phải invalidate (D8)

        var response = await DeleteAsync(category.Id, admin);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var categories = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/v1/categories", JsonOptions);
        categories.Should().NotContain(c => c.Id == category.Id);
        (await _client.GetAsync($"/api/v1/categories/{category.Slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var row = await db.Categories.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == category.Id);
        row.IsDeleted.Should().BeTrue("D2: soft delete, không xóa cứng");
    }

    [Theory(DisplayName = "FR-CAT-005/D2: còn công thức chưa xóa (mọi trạng thái) → 409 CATEGORY_DELETE_HAS_RECIPES, danh mục giữ nguyên")]
    [InlineData(RecipeStatus.Draft)]
    [InlineData(RecipeStatus.Published)]
    [InlineData(RecipeStatus.Archived)]
    public async Task Delete_WithRecipes_Returns409(RecipeStatus status)
    {
        var admin = await RegisterAdminAsync();
        var category = await SeedCategoryAsync();
        await SeedRecipeAsync(category.Id, admin.UserId, status);

        var response = await DeleteAsync(category.Id, admin);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadProblemAsync(response)).Type.Should().Be(ErrorCodes.CategoryDeleteHasRecipes);
        (await _client.GetAsync($"/api/v1/categories/{category.Slug}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-CAT-005/D1,D2: chỉ còn công thức đã xóa mềm → không tính, xóa được → 204")]
    public async Task Delete_OnlySoftDeletedRecipes_Returns204()
    {
        var admin = await RegisterAdminAsync();
        var category = await SeedCategoryAsync();
        await SeedRecipeAsync(category.Id, admin.UserId, RecipeStatus.Published, deleted: true);

        (await DeleteAsync(category.Id, admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "FR-CAT-005: Admin xóa id không tồn tại → 404 CATEGORY_NOT_FOUND")]
    public async Task Delete_UnknownId_Returns404()
    {
        var admin = await RegisterAdminAsync();

        var response = await DeleteAsync(Guid.NewGuid(), admin);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadProblemAsync(response)).Type.Should().Be(ErrorCodes.CategoryNotFound);
    }

    [Fact(DisplayName = "FR-CAT-005: xóa danh mục đã xóa mềm → 404")]
    public async Task Delete_AlreadyDeleted_Returns404()
    {
        var admin = await RegisterAdminAsync();
        var category = await SeedCategoryAsync();
        (await DeleteAsync(category.Id, admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DeleteAsync(category.Id, admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "FR-CAT-005/NFR-SEC-006: Author (không phải Admin) → 403, danh mục giữ nguyên")]
    public async Task Delete_AsAuthor_Returns403()
    {
        var author = await RegisterAsync("cat-del-author");
        var category = await SeedCategoryAsync();

        var response = await DeleteAsync(category.Id, author);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"/api/v1/categories/{category.Slug}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-CAT-005: chưa đăng nhập → 401")]
    public async Task Delete_AsGuest_Returns401()
    {
        var category = await SeedCategoryAsync();

        var response = await _client.DeleteAsync($"/api/v1/categories/{category.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record TestUser(string Token, string UserId);

    private async Task<HttpResponseMessage> DeleteAsync(Guid id, TestUser user)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/categories/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return await _client.SendAsync(request);
    }

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!;

    private async Task<TestUser> RegisterAsync(string prefix)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.com", password = "Str0ng!Pass1", displayName = "Người test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return new TestUser(body!.AccessToken, body.User.Id);
    }

    /// <summary>D11: gán role Admin qua UserManager rồi đăng nhập lại lấy JWT có claim mới.</summary>
    private async Task<TestUser> RegisterAdminAsync()
    {
        var email = $"cat-del-admin-{Guid.NewGuid():N}@example.com";
        const string password = "Str0ng!Pass1";
        (await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Admin test" }))
            .EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(user!, Roles.Admin);
        }

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return new TestUser(body!.AccessToken, body.User.Id);
    }

    private async Task<Category> SeedCategoryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var unique = Guid.NewGuid().ToString("N")[..12];
        var category = Category.Create($"Danh mục xóa {unique}", $"danh-muc-xoa-{unique}", "desc", null, 0);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private async Task SeedRecipeAsync(Guid categoryId, string authorId, RecipeStatus status, bool deleted = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var recipe = Recipe.Create(
            "Công thức trong danh mục", $"recipe-cat-del-{Guid.NewGuid():N}", "mô tả", 10, 30, 2,
            RecipeDifficulty.Easy, categoryId, authorId, status: status);
        if (deleted)
            recipe.SoftDelete();
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
    }
}
