using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Recipes;

/// <summary>FR-RCP-002 — GET /api/v1/recipes/{slug}: nested data, 404, phân quyền Draft (A2), cache D8.</summary>
public class DetailTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task<string> CreateTestUserAsync(IServiceProvider sp, string email)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            return existing.Id;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Test Author",
            EmailConfirmed = true,
            IsActive = true
        };
        await userManager.CreateAsync(user, "Password123!");
        return user.Id;
    }

    [Fact(DisplayName = "FR-RCP-002: Lấy chi tiết công thức Published thành công kèm nested data (200 OK)")]
    public async Task GetRecipeBySlug_Published_Returns200WithFullDetails()
    {
        // 1. Tạo User và Category thật trong DB
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CulinaryBlogDbContext>();

        var authorId = await CreateTestUserAsync(sp, $"author_{Guid.NewGuid():N}@test.com");
        
        var category = Category.Create("Món Việt", "mon-viet-" + Guid.NewGuid().ToString("N")[..6]);
        db.Categories.Add(category);

        var slug = "pho-bo-ha-noi-" + Guid.NewGuid().ToString("N")[..8];
        var recipe = Recipe.Create(
            title: "Phở Bò Hà Nội",
            slug: slug,
            description: "Món phở truyền thống",
            prepTime: 30,
            cookTime: 120,
            servings: 4,
            difficulty: RecipeDifficulty.Medium,
            categoryId: category.Id,
            authorId: authorId,
            status: RecipeStatus.Published
        );
        recipe.AddIngredient("Bánh phở", 500, "g");
        recipe.AddStep(1, "Nấu nước dùng", "Ninh xương bò trong 2 tiếng");
        recipe.AttachImage("https://example.com/pho.jpg", "Ảnh phở", true, 0);

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        // 2. Guest gọi lấy chi tiết
        var response = await _client.GetAsync($"/api/v1/recipes/{slug}");

        // 3. Assert 200 OK và đầy đủ nested data
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RecipeDetailDto>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Phở Bò Hà Nội");
        body.Ingredients.Should().HaveCount(1);
        body.Steps.Should().HaveCount(1);
        body.Images.Should().HaveCount(1);
        body.Author.Id.Should().Be(authorId);
        body.Author.DisplayName.Should().Be("Test Author", "D5 — tên thật của tác giả, không phải chuỗi cố định");
    }

    [Fact(DisplayName = "FR-RCP-002: Slug không tồn tại trả về 404 NOT_FOUND")]
    public async Task GetRecipeBySlug_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/recipes/slug-khong-ton-tai-123");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "FR-RCP-002: Guest hoặc người khác xem Recipe Draft trả về 403 FORBIDDEN")]
    public async Task GetRecipeBySlug_DraftAsGuest_Returns403()
    {
        // 1. Tạo User và Category thật trong DB
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CulinaryBlogDbContext>();

        var authorId = await CreateTestUserAsync(sp, $"draft_author_{Guid.NewGuid():N}@test.com");
        
        var category = Category.Create("Món Canh", "mon-canh-" + Guid.NewGuid().ToString("N")[..6]);
        db.Categories.Add(category);

        var slug = "canh-chua-draft-" + Guid.NewGuid().ToString("N")[..8];
        var recipe = Recipe.Create(
            title: "Canh Chua",
            slug: slug,
            description: "Bản nháp",
            categoryId: category.Id,
            authorId: authorId,
            status: RecipeStatus.Draft
        );
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        // 2. Guest truy cập bản nháp
        var response = await _client.GetAsync($"/api/v1/recipes/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "FR-RCP-002/A2: tác giả xem được bản Draft của chính mình (200)")]
    public async Task GetRecipeBySlug_DraftAsOwner_Returns200()
    {
        var owner = await RegisterAsync();
        var slug = await SeedRecipeAsync(owner.User.Id, RecipeStatus.Draft);

        var response = await GetAsync(slug, owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(JsonOptions))!.Status
            .Should().Be((short)RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-002/A2,NFR-SEC-006: user đăng nhập nhưng không phải tác giả xem Draft → 403")]
    public async Task GetRecipeBySlug_DraftAsOtherUser_Returns403()
    {
        var owner = await RegisterAsync();
        var other = await RegisterAsync();
        var slug = await SeedRecipeAsync(owner.User.Id, RecipeStatus.Draft);

        var response = await GetAsync(slug, other.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "FR-RCP-002/D8,NFR-SEC-006: tác giả xem Draft trước (đã vào cache recipe:{slug}) → khách xem sau vẫn 403")]
    public async Task GetRecipeBySlug_DraftCachedByOwner_GuestStillGets403()
    {
        var owner = await RegisterAsync();
        var slug = await SeedRecipeAsync(owner.User.Id, RecipeStatus.Draft);
        (await GetAsync(slug, owner.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        var guest = await GetAsync(slug, accessToken: null);

        guest.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<AuthResponseDto> RegisterAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"detail-{Guid.NewGuid():N}@example.com", password = "Str0ng!Pass1", displayName = "Tác giả chi tiết" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions))!;
    }

    private async Task<string> SeedRecipeAsync(string authorId, RecipeStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var category = Category.Create("Món thử " + suffix, "mon-thu-" + suffix);
        db.Categories.Add(category);
        var slug = "chi-tiet-" + Guid.NewGuid().ToString("N")[..8];
        db.Recipes.Add(Recipe.Create(title: "Công thức thử", slug: slug, categoryId: category.Id, authorId: authorId, status: status));
        await db.SaveChangesAsync();
        return slug;
    }

    private async Task<HttpResponseMessage> GetAsync(string slug, string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/recipes/{slug}");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _client.SendAsync(request);
    }
}
