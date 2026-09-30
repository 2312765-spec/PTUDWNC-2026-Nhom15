using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Auth;

/// <summary>
/// FR-AUTH-003 — SRS Chương 3 (Google OAuth) + docs/decisions.md D9, D5, D12, D33.
/// Dùng FakeGoogleTokenValidator (đăng ký trong PostgresApiFactory) thay cho Google thật —
/// không thể phát hành ID Token Google hợp lệ trong integration test.
/// </summary>
public sealed class GoogleLoginTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>
    /// Trường hợp khó nhất theo docs/roadmap.md S9: user đã đăng ký thủ công bằng email X,
    /// sau đó đăng nhập Google cũng với email X → PHẢI liên kết (AddLoginAsync), không được
    /// tạo tài khoản thứ hai. D9.
    /// </summary>
    [Fact(DisplayName = "FR-AUTH-003/D9: email đã đăng ký thủ công → Google login LIÊN KẾT, không tạo tài khoản thứ hai")]
    public async Task GoogleLogin_ExistingManualAccount_LinksInsteadOfCreatingSecondAccount()
    {
        var email = $"link-{Guid.NewGuid():N}@example.com";
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "Str0ng!Pass1", displayName = "Người dùng gốc" });
        registerResponse.EnsureSuccessStatusCode();

        var idToken = FakeGoogleTokenValidator.ForUser(email, "Tên Trên Google", "https://google.example/pic.png", Guid.NewGuid().ToString());

        var response = await _client.PostAsJsonAsync("/api/v1/auth/google", new { idToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.User.Email.Should().Be(email);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        (await db.Users.CountAsync(u => u.Email == email)).Should().Be(1);
    }

    [Fact(DisplayName = "FR-AUTH-003/D9,D5: chưa có tài khoản → Google login tự tạo mới, role Author, displayName/avatarUrl từ Google profile")]
    public async Task GoogleLogin_NewEmail_AutoRegistersWithAuthorRole()
    {
        var email = $"newgoogle-{Guid.NewGuid():N}@example.com";
        var idToken = FakeGoogleTokenValidator.ForUser(email, "Người Mới", "https://google.example/new.png", Guid.NewGuid().ToString());

        var response = await _client.PostAsJsonAsync("/api/v1/auth/google", new { idToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.User.Email.Should().Be(email);
        body.User.DisplayName.Should().Be("Người Mới");
        body.User.AvatarUrl.Should().Be("https://google.example/new.png");
        body.User.Roles.Should().ContainSingle().Which.Should().Be("Author");
    }

    [Fact(DisplayName = "FR-AUTH-003/D33 (SRS A1+A2 gộp): ID Token Google không hợp lệ/hết hạn → 400 AUTH_GOOGLE_TOKEN_INVALID")]
    public async Task GoogleLogin_InvalidToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.InvalidToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.AuthGoogleTokenInvalid);
    }

    [Fact(DisplayName = "FR-AUTH-003/D33 (SRS A3): Google API không khả dụng → 502 AUTH_GOOGLE_UNAVAILABLE")]
    public async Task GoogleLogin_GoogleUnavailable_Returns502()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.UnavailableToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.AuthGoogleUnavailable);
    }

    /// <summary>
    /// Chống chiếm tài khoản: Google cho tạo tài khoản với email không phải Gmail mà chưa xác minh
    /// (email_verified = false). Nếu vẫn liên kết theo email, kẻ tấn công vào được tài khoản
    /// đăng ký thủ công của nạn nhân mà không cần mật khẩu.
    /// </summary>
    [Fact(DisplayName = "FR-AUTH-003/NFR-SEC: email Google chưa xác minh + trùng tài khoản có sẵn → 400, KHÔNG liên kết")]
    public async Task GoogleLogin_UnverifiedEmail_ExistingAccount_Returns400AndDoesNotLink()
    {
        var email = $"victim-{Guid.NewGuid():N}@example.com";
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "Str0ng!Pass1", displayName = "Nạn nhân" });
        registerResponse.EnsureSuccessStatusCode();

        var idToken = FakeGoogleTokenValidator.ForUser(email, "Kẻ tấn công", null, Guid.NewGuid().ToString(), emailVerified: false);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/google", new { idToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.AuthGoogleTokenInvalid);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        (await db.UserLogins.CountAsync(l => l.UserId == userId)).Should().Be(0);
    }

    [Fact(DisplayName = "FR-AUTH-003/NFR-SEC: email Google chưa xác minh, chưa có tài khoản → 400, không tạo user")]
    public async Task GoogleLogin_UnverifiedEmail_NewAccount_Returns400()
    {
        var email = $"unverified-{Guid.NewGuid():N}@example.com";
        var idToken = FakeGoogleTokenValidator.ForUser(email, "Ai đó", null, Guid.NewGuid().ToString(), emailVerified: false);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/google", new { idToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        (await db.Users.CountAsync(u => u.Email == email)).Should().Be(0);
    }

    /// <summary>
    /// Google ID (sub) là định danh bất biến; email trên Google có thể đổi. Phải tìm theo sub trước,
    /// nếu không user đổi email Google sẽ bị tạo tài khoản thứ hai.
    /// </summary>
    [Fact(DisplayName = "FR-AUTH-003/D9: cùng Google ID nhưng email Google đã đổi → vẫn đăng nhập đúng tài khoản cũ")]
    public async Task GoogleLogin_SameProviderKey_ChangedEmail_ReturnsSameAccount()
    {
        var providerKey = Guid.NewGuid().ToString();
        var oldEmail = $"old-{Guid.NewGuid():N}@example.com";
        var newEmail = $"new-{Guid.NewGuid():N}@example.com";

        var first = await _client.PostAsJsonAsync(
            "/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.ForUser(oldEmail, "Người dùng", null, providerKey) });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);

        var second = await _client.PostAsJsonAsync(
            "/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.ForUser(newEmail, "Người dùng", null, providerKey) });

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        secondBody!.User.Id.Should().Be(firstBody!.User.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        (await db.Users.CountAsync(u => u.Email == newEmail)).Should().Be(0);
    }

    [Fact(DisplayName = "D4: idToken rỗng → 400 VALIDATION_ERROR (không phải 422)")]
    public async Task GoogleLogin_EmptyIdToken_Returns400ValidationError()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }
}
