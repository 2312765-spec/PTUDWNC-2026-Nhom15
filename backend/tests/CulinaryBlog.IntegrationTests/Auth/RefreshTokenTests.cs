using System.Net;
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
/// FR-AUTH-004 — SRS Chương 3 (làm mới token) + NFR-SEC-002 + docs/decisions.md D4, D11, D20,
/// D24, D25 và D35 (A1/A4 → AUTH_TOKEN_INVALID, reuse → revoke mọi RT còn hiệu lực của user).
/// </summary>
public sealed class RefreshTokenTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string Password = "Str0ng!Pass1";
    private const string RefreshUrl = "/api/v1/auth/refresh";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-AUTH-004/D24: refresh token hợp lệ → 200 kèm AuthResponseDto với cặp token mới")]
    public async Task Refresh_ValidToken_Returns200WithNewTokenPair()
    {
        var (email, login) = await RegisterAndLoginAsync();

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace().And.NotBe(login.RefreshToken);
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        body.User.Email.Should().Be(email);
        body.User.Roles.Should().ContainSingle().Which.Should().Be("Author");
    }

    [Fact(DisplayName = "FR-AUTH-004/D20: rotation — RT cũ bị revoke, ReplacedByTokenHash trỏ tới hash của RT mới")]
    public async Task Refresh_ValidToken_RevokesOldTokenAndLinksReplacement()
    {
        var (_, login) = await RegisterAndLoginAsync();

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);

        var oldToken = await FindTokenAsync(login.RefreshToken);
        var newToken = await FindTokenAsync(body!.RefreshToken);

        oldToken.Should().NotBeNull();
        oldToken!.RevokedAt.Should().NotBeNull();
        oldToken.ReplacedByTokenHash.Should().Be(Hash(body.RefreshToken));

        newToken.Should().NotBeNull();
        newToken!.RevokedAt.Should().BeNull();
        newToken.UserId.Should().Be(oldToken.UserId);
        newToken.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
    }

    [Fact(DisplayName = "FR-AUTH-004/D20,D25: DB chỉ lưu SHA-256 hash của RT mới, không lưu raw token")]
    public async Task Refresh_ValidToken_StoresOnlyHashOfNewToken()
    {
        var (_, login) = await RegisterAndLoginAsync();

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        (await db.RefreshTokens.AnyAsync(t => t.TokenHash == body!.RefreshToken)).Should().BeFalse();
        (await db.RefreshTokens.AnyAsync(t => t.TokenHash == Hash(body!.RefreshToken))).Should().BeTrue();
    }

    [Fact(DisplayName = "FR-AUTH-004: RT mới nhận được dùng tiếp để refresh lần nữa → 200")]
    public async Task Refresh_ChainedRefresh_Succeeds()
    {
        var (_, login) = await RegisterAndLoginAsync();

        var first = await RefreshAsync(login.RefreshToken);
        var secondResponse = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = first.RefreshToken });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-AUTH-004/A1,D35: RT không tồn tại trong DB → 401 AUTH_TOKEN_INVALID")]
    public async Task Refresh_UnknownToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            RefreshUrl,
            new { refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.AuthTokenInvalid);
    }

    [Fact(DisplayName = "FR-AUTH-004/A2: RT đã hết hạn → 401 AUTH_REFRESH_TOKEN_EXPIRED")]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        var (email, _) = await RegisterAndLoginAsync();
        var rawToken = await SeedRefreshTokenAsync(email, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = rawToken });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.AuthRefreshTokenExpired);
    }

    [Fact(DisplayName = "FR-AUTH-004/A3,D20: dùng lại RT đã rotation (reuse) → 401 AUTH_REFRESH_TOKEN_REVOKED")]
    public async Task Refresh_ReusedToken_Returns401()
    {
        var (_, login) = await RegisterAndLoginAsync();
        await RefreshAsync(login.RefreshToken);

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.AuthRefreshTokenRevoked);
    }

    [Fact(DisplayName = "FR-AUTH-004/A3,NFR-SEC-002,D35: reuse → revoke mọi RT còn hiệu lực của user, RT mới nhất cũng hết dùng được")]
    public async Task Refresh_ReusedToken_RevokesAllActiveTokensOfUser()
    {
        var (email, login) = await RegisterAndLoginAsync();
        var rotated = await RefreshAsync(login.RefreshToken);
        var otherDevice = await LoginAsync(email);

        var reuse = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var withRotated = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = rotated.RefreshToken });
        await AssertProblemAsync(withRotated, HttpStatusCode.Unauthorized, ErrorCodes.AuthRefreshTokenRevoked);

        var withOtherDevice = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = otherDevice.RefreshToken });
        await AssertProblemAsync(withOtherDevice, HttpStatusCode.Unauthorized, ErrorCodes.AuthRefreshTokenRevoked);
    }

    [Fact(DisplayName = "FR-AUTH-004/D35: RT vừa revoke vừa hết hạn → ưu tiên AUTH_REFRESH_TOKEN_REVOKED (reuse detection chạy trước)")]
    public async Task Refresh_RevokedAndExpiredToken_ReportsRevoked()
    {
        var (email, _) = await RegisterAndLoginAsync();
        var rawToken = await SeedRefreshTokenAsync(email, expiresAt: DateTime.UtcNow.AddMinutes(-1), revoked: true);

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = rawToken });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.AuthRefreshTokenRevoked);
    }

    [Fact(DisplayName = "FR-AUTH-004/A4,D11: tài khoản IsActive = false sau khi cấp RT → 403 AUTH_ACCOUNT_DISABLED")]
    public async Task Refresh_DisabledAccount_Returns403()
    {
        var (email, login) = await RegisterAndLoginAsync();
        await SetIsActiveAsync(email, isActive: false);

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, ErrorCodes.AuthAccountDisabled);
    }

    [Fact(DisplayName = "FR-AUTH-004/A4,D35: user bị xóa sau khi cấp RT → 401 AUTH_TOKEN_INVALID")]
    public async Task Refresh_DeletedUser_Returns401()
    {
        var (email, login) = await RegisterAndLoginAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.DeleteAsync(user!);
        }

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.AuthTokenInvalid);
    }

    [Fact(DisplayName = "FR-AUTH-004/D35: user đang bị lockout (sai mật khẩu 5 lần) vẫn refresh được bằng RT hợp lệ")]
    public async Task Refresh_LockedOutUser_StillSucceeds()
    {
        var (email, login) = await RegisterAndLoginAsync();
        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Wrong!Pass1" });
        }

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "FR-AUTH-004/D4: thiếu hoặc rỗng refreshToken → 400 VALIDATION_ERROR (không phải 422)")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refresh_EmptyToken_Returns400(string refreshToken)
    {
        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-AUTH-004: endpoint cho phép gọi không cần Bearer token (access token có thể đã hết hạn)")]
    public async Task Refresh_WithoutAuthorizationHeader_IsAllowed()
    {
        var (_, login) = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization.Should().BeNull();

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(string Email, AuthResponseDto Login)> RegisterAndLoginAsync()
    {
        var email = $"refresh-{Guid.NewGuid():N}@example.com";

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

    private async Task<AuthResponseDto> RefreshAsync(string refreshToken)
    {
        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions))!;
    }

    /// <summary>Seed thẳng vào DB để có RT ở trạng thái mà luồng API bình thường không tạo ra được (hết hạn).</summary>
    private async Task<string> SeedRefreshTokenAsync(string email, DateTime expiresAt, bool revoked = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        var (rawToken, tokenHash, _) = jwtService.GenerateRefreshToken();
        var token = RefreshToken.Create(userId, tokenHash, expiresAt);
        if (revoked)
        {
            token.Revoke();
        }

        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();
        return rawToken;
    }

    private async Task<RefreshToken?> FindTokenAsync(string rawToken)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var hash = Hash(rawToken);
        return await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash);
    }

    private string Hash(string rawToken)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IJwtService>().HashToken(rawToken);
    }

    /// <summary>D11: không có endpoint Admin quản lý user — mô phỏng bằng thao tác DB trực tiếp (xem LoginTests).</summary>
    private async Task SetIsActiveAsync(string email, bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        response.StatusCode.Should().Be(status);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem!.Type.Should().Be(errorCode);
    }
}
