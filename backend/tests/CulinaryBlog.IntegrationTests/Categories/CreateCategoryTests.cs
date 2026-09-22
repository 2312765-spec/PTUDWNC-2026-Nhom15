using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Categories;

/// <summary>FR-CAT-003 — SRS mục 3.2/8.2 + docs/decisions.md D10 (auto-suffix slug).</summary>
public sealed class CreateCategoryTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-CAT-003: Admin tạo danh mục hợp lệ → 201 kèm slug tự sinh")]
    public async Task Create_AsAdmin_Returns201()
    {
        var token = await RegisterAdminAsync();
        var name = $"Món test {Guid.NewGuid():N}"[..30];

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { name, description = "mô tả" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        body!.Name.Should().Be(name);
        body.Slug.Should().NotBeNullOrWhiteSpace();
        body.RecipeCount.Should().Be(0);
    }

    [Fact(DisplayName = "FR-CAT-003: không phải Admin (Author thường) → 403")]
    public async Task Create_AsAuthor_Returns403()
    {
        var (token, _) = await RegisterAuthorAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { name = "Món khác", description = (string?)null }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "FR-CAT-003: chưa đăng nhập → 401")]
    public async Task Create_NoToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new { name = "Món ẩn danh", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-CAT-003/D4: tên đã tồn tại → 409 CATEGORY_NAME_EXISTS")]
    public async Task Create_DuplicateName_Returns409()
    {
        var token = await RegisterAdminAsync();
        var name = $"Mon Trung {Guid.NewGuid():N}"[..25];
        await CreateAsync(token, name);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { name, description = (string?)null }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.CategoryNameExists);
    }

    [Fact(DisplayName = "FR-CAT-003/D4: tên rỗng → 400 VALIDATION_ERROR")]
    public async Task Create_EmptyName_Returns400()
    {
        var token = await RegisterAdminAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { name = "", description = (string?)null }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }

    private async Task<CategoryDto> CreateAsync(string token, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { name, description = (string?)null }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
    }

    private async Task<(string Token, string UserId)> RegisterAuthorAsync()
    {
        var email = $"cat-author-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "Str0ng!Pass1", displayName = "Tác giả test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return (body!.AccessToken, body.User.Id);
    }

    /// <summary>D11: gán role Admin thẳng qua UserManager rồi đăng nhập lại lấy JWT có claim mới.</summary>
    private async Task<string> RegisterAdminAsync()
    {
        var email = $"cat-admin-{Guid.NewGuid():N}@example.com";
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
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return body!.AccessToken;
    }
}
