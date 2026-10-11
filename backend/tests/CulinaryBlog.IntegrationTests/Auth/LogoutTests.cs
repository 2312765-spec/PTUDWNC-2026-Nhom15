using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
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
/// FR-AUTH-005 — SRS Chương 3 (đăng xuất / thu hồi refresh token) + docs/decisions.md D4, D20, D35
/// và D47 (đề xuất, xem docs/plans/FR-AUTH-005-dang-xuat-thu-hoi-token.md): giữ RequireAuthorization,
/// token của user khác → 204 không revoke, token đã revoke → 204, thiếu refreshToken → 400.
/// </summary>
public sealed class LogoutTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string Password = "Str0ng!Pass1";
    private const string LogoutUrl = "/api/v1/auth/logout";
    private const string RefreshUrl = "/api/v1/auth/refresh";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-AUTH-005: refresh token hợp lệ → 204 No Content")]
    public async Task Logout_ValidToken_Returns204()
    {
        var (_, login) = await RegisterAndLoginAsync();

        var response = await LogoutAsync(login.AccessToken, login.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "FR-AUTH-005/D20: logout đặt RevokedAt trong DB (IsRevoked computed, không có cột riêng)")]
    public async Task Logout_ValidToken_SetsRevokedAtInDatabase()
    {
        var (_, login) = await RegisterAndLoginAsync();
        (await FindTokenAsync(login.RefreshToken))!.RevokedAt.Should().BeNull();

        await LogoutAsync(login.AccessToken, login.RefreshToken);

        var token = await FindTokenAsync(login.RefreshToken);
        token.Should().NotBeNull();
        token!.IsRevoked.Should().BeTrue();
        token.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact(DisplayName = "FR-AUTH-005: sau logout, refresh bằng token đó → 401 AUTH_REFRESH_TOKEN_REVOKED")]
    public async Task Logout_ThenRefreshWithSameToken_Returns401()
    {
        var (_, login) = await RegisterAndLoginAsync();
        await LogoutAsync(login.AccessToken, login.RefreshToken);

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.AuthRefreshTokenRevoked);
    }

    [Fact(DisplayName = "FR-AUTH-005: logout chỉ thu hồi RT được gửi lên — RT của thiết bị khác vẫn dùng được")]
    public async Task Logout_OnlyRevokesGivenToken()
    {
        var (email, device1) = await RegisterAndLoginAsync();
        var device2 = await LoginAsync(email);

        await LogoutAsync(device1.AccessToken, device1.RefreshToken);

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = device2.RefreshToken });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-AUTH-005/A1: refresh token không tồn tại → 204 (idempotent, không lộ trạng thái)")]
    public async Task Logout_UnknownToken_Returns204()
    {
        var (_, login) = await RegisterAndLoginAsync();

        var response = await LogoutAsync(login.AccessToken, Convert.ToBase64String(Guid.NewGuid().ToByteArray()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "FR-AUTH-005: logout hai lần cùng một token → cả hai đều 204")]
    public async Task Logout_Twice_IsIdempotent()
    {
        var (_, login) = await RegisterAndLoginAsync();

        var first = await LogoutAsync(login.AccessToken, login.RefreshToken);
        var second = await LogoutAsync(login.AccessToken, login.RefreshToken);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "FR-AUTH-005/D47: token đã revoke → 204 và giữ nguyên RevokedAt, KHÔNG kích hoạt reuse detection (D35-5)")]
    public async Task Logout_AlreadyRevokedToken_Returns204WithoutRevokingOthers()
    {
        var (email, device1) = await RegisterAndLoginAsync();
        var device2 = await LoginAsync(email);
        await LogoutAsync(device1.AccessToken, device1.RefreshToken);
        var revokedAt = (await FindTokenAsync(device1.RefreshToken))!.RevokedAt;

        var response = await LogoutAsync(device1.AccessToken, device1.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindTokenAsync(device1.RefreshToken))!.RevokedAt.Should().Be(revokedAt);
        (await FindTokenAsync(device2.RefreshToken))!.RevokedAt.Should().BeNull();
    }

    [Fact(DisplayName = "FR-AUTH-005/D47: RT đã hết hạn → 204")]
    public async Task Logout_ExpiredToken_Returns204()
    {
        var (email, login) = await RegisterAndLoginAsync();
        var rawToken = await SeedRefreshTokenAsync(email, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var response = await LogoutAsync(login.AccessToken, rawToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "FR-AUTH-005/NFR-SEC-006,D47: RT của user khác → 204 nhưng KHÔNG bị revoke")]
    public async Task Logout_TokenOfAnotherUser_Returns204AndDoesNotRevoke()
    {
        var (_, attacker) = await RegisterAndLoginAsync();
        var (_, victim) = await RegisterAndLoginAsync();

        var response = await LogoutAsync(attacker.AccessToken, victim.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindTokenAsync(victim.RefreshToken))!.RevokedAt.Should().BeNull();

        var refresh = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = victim.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-AUTH-005/NFR-SEC-006: không có Authorization header → 401")]
    public async Task Logout_WithoutAuthorizationHeader_Returns401()
    {
        var (_, login) = await RegisterAndLoginAsync();

        var response = await _client.PostAsJsonAsync(LogoutUrl, new { refreshToken = login.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await FindTokenAsync(login.RefreshToken))!.RevokedAt.Should().BeNull();
    }

    [Fact(DisplayName = "FR-AUTH-005: access token không hợp lệ → 401, RT không bị revoke")]
    public async Task Logout_WithInvalidAccessToken_Returns401()
    {
        var (_, login) = await RegisterAndLoginAsync();

        var response = await LogoutAsync("not.a.valid.jwt", login.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await FindTokenAsync(login.RefreshToken))!.RevokedAt.Should().BeNull();
    }

    [Theory(DisplayName = "FR-AUTH-005/D4,D47: thiếu hoặc rỗng refreshToken → 400 VALIDATION_ERROR (không phải 422)")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Logout_EmptyToken_Returns400(string refreshToken)
    {
        var (_, login) = await RegisterAndLoginAsync();

        var response = await LogoutAsync(login.AccessToken, refreshToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-AUTH-005/D4,D47: body không có trường refreshToken → 400 VALIDATION_ERROR")]
    public async Task Logout_MissingTokenField_Returns400()
    {
        var (_, login) = await RegisterAndLoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, LogoutUrl)
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await _client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }

    /// <summary>Dùng HttpRequestMessage riêng cho từng lần gọi để không dính header Authorization giữa các user.</summary>
    private async Task<HttpResponseMessage> LogoutAsync(string accessToken, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LogoutUrl)
        {
            Content = JsonContent.Create(new { refreshToken })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<(string Email, AuthResponseDto Login)> RegisterAndLoginAsync()
    {
        var email = $"logout-{Guid.NewGuid():N}@example.com";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Người dùng test",
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, Password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Tạo user test thất bại: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, "Author");
        }

        return (email, await LoginAsync(email));
    }

    private async Task<AuthResponseDto> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions))!;
    }

    /// <summary>Seed thẳng vào DB để có RT ở trạng thái mà luồng API bình thường không tạo ra được (hết hạn).</summary>
    private async Task<string> SeedRefreshTokenAsync(string email, DateTime expiresAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        var (rawToken, tokenHash, _) = jwtService.GenerateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Create(userId, tokenHash, expiresAt));
        await db.SaveChangesAsync();
        return rawToken;
    }

    private async Task<RefreshToken?> FindTokenAsync(string rawToken)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<IJwtService>().HashToken(rawToken);
        return await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        response.StatusCode.Should().Be(status);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem!.Type.Should().Be(errorCode);
    }
}
