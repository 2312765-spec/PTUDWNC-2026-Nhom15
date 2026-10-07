using System.Net;
using System.Net.Http.Json;
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

public class DetailTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
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
}