using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Recipes;

/// <summary>
/// FR-RCP-001 + FR-SRCH-002/003/004 — GET /api/v1/recipes (docs/decisions.md D4, D8, D14).
/// Mỗi test seed vào một Category riêng rồi lọc theo categoryId để không đụng dữ liệu test khác.
/// </summary>
public sealed class GetRecipesTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-RCP-001: Guest chỉ thấy Published, không thấy Draft/Archived")]
    public async Task List_Guest_OnlyPublished()
    {
        var (_, authorId) = await RegisterAuthorAsync();
        var categoryId = await SeedCategoryAsync();
        var published = await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Draft);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Archived);

        var body = await ListAsync($"categoryId={categoryId}");

        body.Items.Select(r => r.Id).Should().Equal(published);
        body.TotalCount.Should().Be(1);
    }

    [Fact(DisplayName = "FR-RCP-001: Author thấy thêm Draft/Archived của mình, không thấy Draft của người khác")]
    public async Task List_Author_SeesOwnNonPublishedOnly()
    {
        var (token, authorId) = await RegisterAuthorAsync();
        var (_, otherId) = await RegisterAuthorAsync();
        var categoryId = await SeedCategoryAsync();
        var published = await SeedRecipeAsync(categoryId, otherId, RecipeStatus.Published);
        var ownDraft = await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Draft);
        var ownArchived = await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Archived);
        await SeedRecipeAsync(categoryId, otherId, RecipeStatus.Draft);

        var body = await ListAsync($"categoryId={categoryId}", token);

        body.Items.Select(r => r.Id).Should().BeEquivalentTo([published, ownDraft, ownArchived]);
    }

    [Fact(DisplayName = "FR-RCP-001/D8: Guest gọi sau Author cùng query không nhận Draft từ cache")]
    public async Task List_CacheIsScopedByViewer()
    {
        var (token, authorId) = await RegisterAuthorAsync();
        var categoryId = await SeedCategoryAsync();
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Draft);

        (await ListAsync($"categoryId={categoryId}", token)).Items.Should().HaveCount(1);
        (await ListAsync($"categoryId={categoryId}")).Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-001: Admin thấy mọi trạng thái")]
    public async Task List_Admin_SeesAllStatuses()
    {
        var (_, authorId) = await RegisterAuthorAsync();
        var adminToken = await RegisterAdminAsync();
        var categoryId = await SeedCategoryAsync();
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Draft);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Archived);

        var body = await ListAsync($"categoryId={categoryId}", adminToken);

        body.TotalCount.Should().Be(3);
    }

    [Fact(DisplayName = "FR-RCP-001: query string isAdmin/currentUserId không vượt quyền được")]
    public async Task List_CannotEscalateViaQueryString()
    {
        var (_, authorId) = await RegisterAuthorAsync();
        var categoryId = await SeedCategoryAsync();
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Draft);

        var body = await ListAsync($"categoryId={categoryId}&isAdmin=true&currentUserId={authorId}");

        body.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-SRCH-002/D14: lọc difficulty + maxCookTime + minServings kết hợp AND")]
    public async Task List_FiltersCombineWithAnd()
    {
        var (_, authorId) = await RegisterAuthorAsync();
        var categoryId = await SeedCategoryAsync();
        var match = await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published, RecipeDifficulty.Expert, cookTime: 20, servings: 4);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published, RecipeDifficulty.Easy, cookTime: 20, servings: 4);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published, RecipeDifficulty.Expert, cookTime: 90, servings: 4);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published, RecipeDifficulty.Expert, cookTime: 20, servings: 1);

        var body = await ListAsync($"categoryId={categoryId}&difficulty=Expert&maxCookTime=30&minServings=2");

        body.Items.Select(r => r.Id).Should().Equal(match);
    }

    [Fact(DisplayName = "FR-SRCH-003: sort=-cookTime sắp giảm dần theo thời gian nấu")]
    public async Task List_SortByCookTimeDesc()
    {
        var (_, authorId) = await RegisterAuthorAsync();
        var categoryId = await SeedCategoryAsync();
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published, cookTime: 10);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published, cookTime: 50);
        await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published, cookTime: 30);

        var body = await ListAsync($"categoryId={categoryId}&sort=-cookTime");

        body.Items.Select(r => r.CookTimeMinutes).Should().Equal(50, 30, 10);
    }

    [Fact(DisplayName = "FR-SRCH-004: phân trang trả totalCount/totalPages/hasNextPage, pageSize > 50 clamp")]
    public async Task List_Paging()
    {
        var (_, authorId) = await RegisterAuthorAsync();
        var categoryId = await SeedCategoryAsync();
        for (var i = 0; i < 3; i++)
          {  await SeedRecipeAsync(categoryId, authorId, RecipeStatus.Published);}

        var page1 = await ListAsync($"categoryId={categoryId}&page=1&pageSize=2");
        var clamped = await ListAsync($"categoryId={categoryId}&pageSize=100");

        page1.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(3);
        page1.TotalPages.Should().Be(2);
        page1.HasNextPage.Should().BeTrue();
        clamped.PageSize.Should().Be(PagedResult<RecipeSummaryDto>.MaxPageSize);
    }

    [Fact(DisplayName = "FR-RCP-001 A2: categoryId không tồn tại → 200 với items rỗng")]
    public async Task List_UnknownCategory_ReturnsEmpty()
    {
        var body = await ListAsync($"categoryId={Guid.NewGuid()}");

        body.Items.Should().BeEmpty();
    }

    [Theory(DisplayName = "FR-RCP-001 A1/D4: tham số không hợp lệ → 400 VALIDATION_ERROR (không 422)")]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("sort=servings")]
    [InlineData("difficulty=Legendary")]
    [InlineData("minServings=0")]
    public async Task List_InvalidParams_Returns400(string queryString)
    {
        var response = await _client.GetAsync($"/api/v1/recipes?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }

    private async Task<PagedResult<RecipeSummaryDto>> ListAsync(string queryString, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/recipes?{queryString}");
        if (token is not null)
            {request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);}

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>(_jsonOptions);
        body.Should().NotBeNull();
        return body!;
    }

    private async Task<(string Token, string UserId)> RegisterAuthorAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"list-{Guid.NewGuid():N}@example.com", password = "Str0ng!Pass1", displayName = "Tác giả test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        return (body!.AccessToken, body.User.Id);
    }

    /// <summary>D11: không có endpoint quản lý user — gán role Admin qua UserManager rồi đăng nhập lại.</summary>
    private async Task<string> RegisterAdminAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        const string password = "Str0ng!Pass1";
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password, displayName = "Admin test" });
        registerResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(user!, Roles.Admin);
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        return body!.AccessToken;
    }

    private async Task<Guid> SeedCategoryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var unique = Guid.NewGuid().ToString("N")[..12];
        var category = Category.Create($"Danh mục list {unique}", $"danh-muc-list-{unique}", "desc", null, 0);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private async Task<Guid> SeedRecipeAsync(
        Guid categoryId,
        string authorId,
        RecipeStatus status,
        RecipeDifficulty difficulty = RecipeDifficulty.Easy,
        int cookTime = 30,
        int servings = 2)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var recipe = Recipe.Create(
            $"Món {Guid.NewGuid():N}", $"recipe-{Guid.NewGuid():N}", "mô tả", 10, cookTime, servings,
            difficulty, categoryId, authorId, status: status);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe.Id;
    }
}
