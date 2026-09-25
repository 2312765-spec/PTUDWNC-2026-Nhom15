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

/// <summary>FR-CAT-004 — SRS mục 3.2/8.2 + docs/decisions.md D4 (validation → 400), D10 (slug bất biến), D8 (cache).</summary>
public sealed class UpdateCategoryTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-CAT-004/D10: Admin đổi tên + mô tả → 200, Slug KHÔNG đổi")]
    public async Task Update_AsAdmin_Returns200AndKeepsSlug()
    {
        var token = await RegisterAdminAsync();
        var created = await CreateAsync(token, $"Mon cu {Guid.NewGuid():N}"[..25]);
        var newName = $"Mon moi {Guid.NewGuid():N}"[..25];

        var response = await PutAsync(token, created.Id, new { name = newName, description = "mô tả mới" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        body!.Id.Should().Be(created.Id);
        body.Name.Should().Be(newName);
        body.Description.Should().Be("mô tả mới");
        body.Slug.Should().Be(created.Slug);
    }

    [Fact(DisplayName = "FR-CAT-004/D8: sau khi cập nhật, GET /categories thấy tên mới (cache đã invalidate)")]
    public async Task Update_InvalidatesCategoriesCache()
    {
        var token = await RegisterAdminAsync();
        var created = await CreateAsync(token, $"Mon cache {Guid.NewGuid():N}"[..25]);
        (await _client.GetAsync("/api/v1/categories")).EnsureSuccessStatusCode();
        var newName = $"Mon doi {Guid.NewGuid():N}"[..25];

        (await PutAsync(token, created.Id, new { name = newName, description = (string?)null })).EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/v1/categories", JsonOptions);
        list!.Should().Contain(c => c.Id == created.Id && c.Name == newName);
    }

    [Fact(DisplayName = "FR-CAT-004: không phải Admin (Author thường) → 403")]
    public async Task Update_AsAuthor_Returns403()
    {
        var adminToken = await RegisterAdminAsync();
        var created = await CreateAsync(adminToken, $"Mon quyen {Guid.NewGuid():N}"[..25]);
        var authorToken = await RegisterAuthorAsync();

        var response = await PutAsync(authorToken, created.Id, new { name = "Ten moi", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "FR-CAT-004: chưa đăng nhập → 401")]
    public async Task Update_NoToken_Returns401()
    {
        var response = await _client.PutAsJsonAsync($"/api/v1/categories/{Guid.NewGuid()}", new { name = "Ten moi", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-CAT-004: ID không tồn tại → 404 CATEGORY_NOT_FOUND")]
    public async Task Update_UnknownId_Returns404()
    {
        var token = await RegisterAdminAsync();

        var response = await PutAsync(token, Guid.NewGuid(), new { name = "Ten moi", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.CategoryNotFound);
    }

    [Fact(DisplayName = "FR-CAT-004/D4: đổi sang tên của danh mục khác → 409 CATEGORY_NAME_EXISTS")]
    public async Task Update_DuplicateName_Returns409()
    {
        var token = await RegisterAdminAsync();
        var other = await CreateAsync(token, $"Mon khac {Guid.NewGuid():N}"[..25]);
        var target = await CreateAsync(token, $"Mon dich {Guid.NewGuid():N}"[..25]);

        var response = await PutAsync(token, target.Id, new { name = other.Name, description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.CategoryNameExists);
    }

    [Fact(DisplayName = "FR-CAT-004: giữ nguyên tên, chỉ đổi mô tả → 200 (không tự trùng với chính nó)")]
    public async Task Update_SameName_Returns200()
    {
        var token = await RegisterAdminAsync();
        var created = await CreateAsync(token, $"Mon giu {Guid.NewGuid():N}"[..25]);

        var response = await PutAsync(token, created.Id, new { name = created.Name, description = "chỉ đổi mô tả" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-CAT-004/D4: tên rỗng → 400 VALIDATION_ERROR")]
    public async Task Update_EmptyName_Returns400()
    {
        var token = await RegisterAdminAsync();
        var created = await CreateAsync(token, $"Mon rong {Guid.NewGuid():N}"[..25]);

        var response = await PutAsync(token, created.Id, new { name = "", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-CAT-004/D4: body thiếu name → 400 VALIDATION_ERROR, không phải 500")]
    public async Task Update_MissingName_Returns400()
    {
        var token = await RegisterAdminAsync();
        var created = await CreateAsync(token, $"Mon thieu {Guid.NewGuid():N}"[..25]);

        var response = await PutAsync(token, created.Id, new { description = "chỉ có mô tả" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-CAT-004/D4: mô tả chứa HTML → 400 VALIDATION_ERROR")]
    public async Task Update_DescriptionWithHtml_Returns400()
    {
        var token = await RegisterAdminAsync();
        var created = await CreateAsync(token, $"Mon html {Guid.NewGuid():N}"[..25]);

        var response = await PutAsync(token, created.Id, new { name = created.Name, description = "<script>alert(1)</script>" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }

    private Task<HttpResponseMessage> PutAsync(string token, Guid id, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/categories/{id}")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
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

    private async Task<string> RegisterAuthorAsync()
    {
        var email = $"cat-author-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "Str0ng!Pass1", displayName = "Tác giả test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return body!.AccessToken;
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
