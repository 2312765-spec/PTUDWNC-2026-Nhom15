using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Auth;

/// <summary>
/// FR-AUTH-002 — SRS Chương 3 (đăng nhập) + docs/decisions.md D4, D11, D17, D20, D24.
/// </summary>
public sealed class LoginTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> RegisterUserAsync(string password = "Str0ng!Pass1")
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password, displayName = "Người dùng test" });
        response.EnsureSuccessStatusCode();
        return email;
    }

    [Fact(DisplayName = "FR-AUTH-002/D24: đăng nhập đúng email/password → 200 kèm AuthResponseDto đầy đủ token")]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        const string password = "Str0ng!Pass1";
        var email = await RegisterUserAsync(password);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.User.Email.Should().Be(email);
        body.User.Roles.Should().ContainSingle().Which.Should().Be("Author");
    }

    [Fact(DisplayName = "FR-AUTH-002/A1: sai mật khẩu → 401 AUTH_INVALID_CREDENTIALS (thông điệp chung)")]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = await RegisterUserAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Wrong!Pass1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.AuthInvalidCredentials);
    }

    [Fact(DisplayName = "FR-AUTH-002/A1: email không tồn tại → 401 AUTH_INVALID_CREDENTIALS (không tiết lộ tài khoản có tồn tại)")]
    public async Task Login_UnknownEmail_Returns401SameAsWrongPassword()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = $"khong-ton-tai-{Guid.NewGuid():N}@example.com", password = "Bat-Ky-Gi-1!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.AuthInvalidCredentials);
    }

    [Fact(DisplayName = "FR-AUTH-002/D11: tài khoản IsActive = false → 403 AUTH_ACCOUNT_DISABLED")]
    public async Task Login_DisabledAccount_Returns403()
    {
        const string password = "Str0ng!Pass1";
        var email = await RegisterUserAsync(password);
        await SetIsActiveAsync(email, isActive: false);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.AuthAccountDisabled);
    }

    [Fact(DisplayName = "FR-AUTH-002/A3,D17: sai mật khẩu 5 lần liên tiếp → lần thứ 6 (dù đúng mật khẩu) trả 423 AUTH_ACCOUNT_LOCKED")]
    public async Task Login_FiveFailedAttempts_ThenLocksAccount()
    {
        const string password = "Str0ng!Pass1";
        var email = await RegisterUserAsync(password);

        for (var i = 0; i < 5; i++)
        {
            var failedResponse = await _client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email, password = "Wrong!Pass1" });
            failedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        response.StatusCode.Should().Be((HttpStatusCode)423);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.AuthAccountLocked);
    }

    [Theory(DisplayName = "D4: dữ liệu không hợp lệ → 400 VALIDATION_ERROR (không phải 422)")]
    [InlineData("not-an-email", "any-password")]
    [InlineData("valid@example.com", "")]
    public async Task Login_InvalidInput_Returns400(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }

    /// <summary>
    /// D11: không có endpoint Admin quản lý user (ngoài scope v1) — test tự thao tác DB trực
    /// tiếp để mô phỏng "Admin đã vô hiệu hóa tài khoản", đúng như decisions.md mô tả cách
    /// vận hành thật của tính năng này trong v1.
    /// </summary>
    private async Task SetIsActiveAsync(string email, bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.IsActive = isActive;
        await db.SaveChangesAsync();
    }
}
