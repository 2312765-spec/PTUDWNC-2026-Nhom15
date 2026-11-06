using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.IntegrationTests.Recipes.Support;

/// <summary>
/// Hỗ trợ chung cho integration test lệnh ghi recipe (FR-RCP-003/004/010): đăng ký Author/Admin thật
/// qua API, tạo category trong DB, gửi request kèm Bearer token.
/// </summary>
public sealed class RecipeCommandClient(PostgresApiFactory factory)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpClient Http { get; } = factory.CreateClient();

    public sealed record TestUser(string Token, string UserId);

    public async Task<TestUser> RegisterAuthorAsync()
    {
        var response = await Http.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"rcp-{Guid.NewGuid():N}@example.com", password = "Str0ng!Pass1", displayName = "Tác giả recipe" });
        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions))!;
        return new TestUser(body.AccessToken, body.User.Id);
    }

    /// <summary>D11: gán role Admin thẳng qua UserManager rồi đăng nhập lại lấy JWT có claim mới.</summary>
    public async Task<TestUser> RegisterAdminAsync()
    {
        var email = $"rcp-admin-{Guid.NewGuid():N}@example.com";
        const string password = "Str0ng!Pass1";
        (await Http.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Admin recipe" }))
            .EnsureSuccessStatusCode();

        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await userManager.FindByEmailAsync(email))!;
            await userManager.AddToRoleAsync(user, Roles.Admin);
            userId = user.Id;
        }

        var login = await Http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var body = (await login.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions))!;
        return new TestUser(body.AccessToken, userId);
    }

    public async Task<Category> CreateCategoryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var category = Category.Create("Danh mục " + suffix, "danh-muc-" + suffix);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public static object CreateBody(Guid categoryId, string title = "Gỏi cuốn tôm thịt", object? steps = null, object? ingredients = null) => new
    {
        title,
        description = "Món cuốn tươi mát của miền Nam",
        categoryId,
        prepTime = 20,
        cookTime = 10,
        servings = 4,
        difficulty = "Easy",
        instructions = (string?)null,
        nutrition = new { calories = 250m, protein = 12m, carbohydrates = 30m, fat = 8m },
        steps = steps ?? new[]
        {
            new { title = "Sơ chế", description = "Luộc tôm và thịt", timerMinutes = (int?)10 },
            new { title = "Cuốn", description = "Cuốn bánh tráng", timerMinutes = (int?)null },
        },
        ingredients = ingredients ?? new[]
        {
            new { name = "Tôm", quantity = (decimal?)300m, unit = (string?)"g", notes = (string?)null },
            new { name = "Muối", quantity = (decimal?)null, unit = (string?)null, notes = (string?)"vừa đủ" },
        },
    };

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, string? token, object? body = null, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await Http.SendAsync(request);
    }

    /// <summary>Tạo recipe qua API (FR-RCP-003) và trả DTO — dùng làm dữ liệu đầu vào cho test FR khác.</summary>
    public async Task<RecipeDetailDto> CreateRecipeAsync(TestUser user, Guid categoryId, object? steps = null, object? ingredients = null)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/recipes", user.Token, CreateBody(categoryId, steps: steps, ingredients: ingredients));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(JsonOptions))!;
    }
}
