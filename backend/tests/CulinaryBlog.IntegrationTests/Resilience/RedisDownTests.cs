using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Resilience.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Resilience;

/// <summary>
/// NFR-REL-002 (D8, D90) — chaos test: tắt Redis giữa chừng, API vẫn phục vụ từ PostgreSQL,
/// không chậm bất thường, và không trả cache cũ khi Redis lên lại.
/// Mỗi test bắt đầu với Redis đang chạy (InitializeAsync) và luôn bật lại Redis khi kết thúc.
/// </summary>
public sealed class RedisDownTests(RedisChaosApiFactory factory)
    : IClassFixture<RedisChaosApiFactory>, IAsyncLifetime
{
    /// <summary>
    /// Ngưỡng cho MỘT request khi Redis tắt. Request bình thường ~vài chục ms; StackExchange.Redis
    /// mặc định chờ 5 s mới báo lỗi — ngưỡng này bắt đúng trường hợp đó.
    /// </summary>
    private static readonly TimeSpan MaxLatencyWhenRedisDown = TimeSpan.FromSeconds(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        if (!factory.RedisRunning)
            await factory.StartRedisAsync();
    }

    public async Task DisposeAsync()
    {
        if (!factory.RedisRunning)
            await factory.StartRedisAsync();
    }

    [Fact(DisplayName = "NFR-REL-002/D8: Redis tắt → các GET có cache vẫn 200 từ DB, mỗi request < 1 s")]
    public async Task RedisDown_CachedQueries_Return200Fast()
    {
        var token = $"chaos{Guid.NewGuid():N}"[..14];
        var slug = await SeedPublishedRecipeAsync($"Canh chua {token}");
        string[] urls =
        [
            "/api/v1/categories",
            "/api/v1/recipes",
            $"/api/v1/recipes/{slug}",
            $"/api/v1/recipes/search?q={token}",
        ];

        // Nạp cache khi Redis còn sống — sau khi tắt, API phải tự bỏ qua cache chứ không trả lỗi.
        foreach (var url in urls)
            (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK, url);

        await factory.StopRedisAsync();

        foreach (var url in urls)
        {
            // Hai lượt: lượt đầu có thể là lúc multiplexer mới phát hiện mất kết nối.
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var sw = Stopwatch.StartNew();
                var response = await _client.GetAsync(url);
                sw.Stop();

                response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} (lượt {attempt})");
                sw.Elapsed.Should().BeLessThan(MaxLatencyWhenRedisDown, $"{url} (lượt {attempt}) không được treo chờ Redis");
            }
        }

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/recipes/{slug}", JsonOptions);
        detail.GetProperty("slug").GetString().Should().Be(slug);
    }

    [Fact(DisplayName = "NFR-REL-002/D8: Redis tắt → Admin tạo danh mục vẫn 201, đọc lại thấy ngay")]
    public async Task RedisDown_CreateCategory_Returns201()
    {
        var adminToken = await RegisterAdminAsync();
        await factory.StopRedisAsync();

        var name = $"Chaos tạo {Guid.NewGuid():N}"[..24];
        var sw = Stopwatch.StartNew();
        var created = await CreateCategoryAsync(adminToken, name);
        sw.Stop();

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        sw.Elapsed.Should().BeLessThan(MaxLatencyWhenRedisDown);

        var categories = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/v1/categories", JsonOptions);
        categories.Should().Contain(c => c.Name == name);
    }

    [Fact(DisplayName = "NFR-REL-002/FR-OBS-001: Redis tắt → /health/live 200, /health/ready 503")]
    public async Task RedisDown_Health_LiveOk_ReadyUnavailable()
    {
        await factory.StopRedisAsync();

        (await _client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "NFR-REL-002/D90: ghi lúc Redis tắt → Redis lên lại không trả cache cũ")]
    public async Task RedisRestored_AfterWriteWhileDown_DoesNotServeStaleCache()
    {
        var adminToken = await RegisterAdminAsync();

        // Nạp "categories:all" (TTL 30 phút) khi Redis sống.
        (await _client.GetAsync("/api/v1/categories")).EnsureSuccessStatusCode();
        (await factory.RedisKeyExistsAsync("categories:all")).Should().BeTrue("cache phải được nạp trước khi tắt Redis");

        await factory.StopRedisAsync();
        var name = $"Chaos cũ {Guid.NewGuid():N}"[..22];
        (await CreateCategoryAsync(adminToken, name)).StatusCode.Should().Be(HttpStatusCode.Created);

        await factory.StartRedisAsync();

        // Việc dọn cache chạy bất đồng bộ sau sự kiện kết nối lại — cho tối đa 10 s.
        // Không sửa thì bản cũ còn tới 30 phút, nên chờ 10 s vẫn phân biệt rõ đúng/sai.
        var sw = Stopwatch.StartNew();
        List<CategoryDto>? categories;
        do
        {
            categories = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/v1/categories", JsonOptions);
            if (categories!.Any(c => c.Name == name))
                break;
            await Task.Delay(200);
        }
        while (sw.Elapsed < TimeSpan.FromSeconds(10));

        categories.Should().Contain(c => c.Name == name, "danh mục tạo lúc Redis tắt phải thấy sau khi Redis lên lại");
    }

    [Fact(DisplayName = "NFR-REL-002/D8: Redis lên lại → cache hoạt động tiếp, không cần khởi động lại API")]
    public async Task RedisRestored_CacheWorksAgain()
    {
        await factory.StopRedisAsync();
        (await _client.GetAsync("/api/v1/categories")).EnsureSuccessStatusCode();

        await factory.StartRedisAsync();

        var sw = Stopwatch.StartNew();
        var cached = false;
        while (!cached && sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            (await _client.GetAsync("/api/v1/categories")).EnsureSuccessStatusCode();
            cached = await factory.RedisKeyExistsAsync("categories:all");
            if (!cached)
                await Task.Delay(200);
        }

        cached.Should().BeTrue("GET /categories sau khi Redis lên lại phải ghi lại key categories:all");
    }

    private async Task<HttpResponseMessage> CreateCategoryAsync(string adminToken, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { name, description = (string?)null }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        return await _client.SendAsync(request);
    }

    private async Task<string> RegisterAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password, displayName = "Chaos test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return body!.User.Id;
    }

    /// <summary>D11: gán role Admin qua UserManager rồi đăng nhập lại lấy JWT có claim mới.</summary>
    private async Task<string> RegisterAdminAsync()
    {
        var email = $"chaos-admin-{Guid.NewGuid():N}@example.com";
        const string password = "Str0ng!Pass1";
        await RegisterAsync(email, password);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(user!, Roles.Admin);
        }

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions))!.AccessToken;
    }

    private async Task<string> SeedPublishedRecipeAsync(string title)
    {
        var authorId = await RegisterAsync($"chaos-author-{Guid.NewGuid():N}@example.com", "Str0ng!Pass1");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var unique = Guid.NewGuid().ToString("N")[..12];
        var category = Category.Create($"Danh mục chaos {unique}", $"danh-muc-chaos-{unique}", "desc", null, 0);
        db.Categories.Add(category);

        var slug = $"recipe-chaos-{unique}";
        var recipe = Recipe.Create(
            title, slug, "mô tả", 10, 30, 2,
            RecipeDifficulty.Easy, category.Id, authorId, status: RecipeStatus.Published);
        db.Recipes.Add(recipe);

        await db.SaveChangesAsync();
        return slug;
    }
}
