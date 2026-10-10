using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Auth;

/// <summary>
/// FR-AUTH-006 — SRS Chương 3 (xem hồ sơ cá nhân) + docs/decisions.md D5, D12, D4 và D48:
/// response chỉ gồm { id, email, displayName, avatarUrl, bio, roles }; user bị xóa → 404 USER_NOT_FOUND;
/// user IsActive=false còn token → 200. Xem docs/plans/FR-AUTH-006-xem-ho-so-ca-nhan.md.
/// </summary>
public sealed class ProfileTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string Password = "Str0ng!Pass1";
    private const string MeUrl = "/api/v1/auth/me";
    private const string UserNotFound = "USER_NOT_FOUND"; // D48 — chưa có trong ErrorCodes (tạo ở bước code)

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] _d5Fields = ["id", "email", "displayName", "avatarUrl", "bio", "roles"];

    private static readonly string[] _forbiddenFields =
    [
        "fullName", "userName", "emailConfirmed", "createdAt",
        "passwordHash", "securityStamp", "normalizedEmail", "normalizedUserName", "concurrencyStamp"
    ];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-AUTH-006: token hợp lệ → 200 với hồ sơ đúng của user hiện tại")]
    public async Task GetMe_ValidToken_Returns200WithProfile()
    {
        var seeded = await SeedUserAsync(displayName: "Đầu Bếp Test", avatarUrl: "https://example.com/a.png", bio: "Thích nấu ăn");
        var login = await LoginAsync(seeded.Email);

        var response = await GetMeAsync(login.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        profile.Should().NotBeNull();
        profile!.Id.Should().Be(seeded.Id);
        profile.Email.Should().Be(seeded.Email);
        profile.DisplayName.Should().Be("Đầu Bếp Test");
        profile.AvatarUrl.Should().Be("https://example.com/a.png");
        profile.Bio.Should().Be("Thích nấu ăn");
    }

    [Fact(DisplayName = "FR-AUTH-006/D5: response có đúng 6 trường { id, email, displayName, avatarUrl, bio, roles }")]
    public async Task GetMe_ResponseContainsExactlyD5Fields()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await GetMeAsync(login.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        names.Should().BeEquivalentTo(_d5Fields);
    }

    [Fact(DisplayName = "FR-AUTH-006/D5,D12: không có fullName/userName/emailConfirmed/createdAt, không lộ PasswordHash/SecurityStamp")]
    public async Task GetMe_ResponseHasNoForbiddenOrSensitiveFields()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await GetMeAsync(login.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var names = json.RootElement.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToHashSet();
        foreach (var field in _forbiddenFields)
        {
            names.Should().NotContain(field.ToLowerInvariant(), $"D5/D12 loại '{field}' khỏi response");
        }

        body.Should().NotContain("PasswordHash", because: "không bao giờ trả hash mật khẩu");
        body.Should().NotContain("AQAAAA", because: "chuỗi hash Identity v3 bắt đầu bằng AQAAAA");
    }

    [Fact(DisplayName = "FR-AUTH-006/D5: avatarUrl và bio chưa đặt → null (trường vẫn có mặt)")]
    public async Task GetMe_NoAvatarNoBio_ReturnsNullFields()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await GetMeAsync(login.AccessToken);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.GetProperty("bio").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory(DisplayName = "FR-AUTH-006: roles phản ánh đúng vai trò của user")]
    [InlineData("Author")]
    [InlineData("Admin")]
    public async Task GetMe_ReturnsRolesOfUser(string role)
    {
        var seeded = await SeedUserAsync(role: role);
        var login = await LoginAsync(seeded.Email);

        var response = await GetMeAsync(login.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        profile!.Roles.Should().BeEquivalentTo([role]);
    }

    [Fact(DisplayName = "FR-AUTH-006: /me trả cùng hồ sơ với user trong AuthResponse của login (D24)")]
    public async Task GetMe_MatchesUserFromLoginResponse()
    {
        var seeded = await SeedUserAsync(bio: "Bio");
        var login = await LoginAsync(seeded.Email);

        var response = await GetMeAsync(login.AccessToken);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        profile.Should().BeEquivalentTo(login.User);
    }

    [Fact(DisplayName = "FR-AUTH-006/NFR-SEC-006: không có Authorization header → 401")]
    public async Task GetMe_WithoutAuthorizationHeader_Returns401()
    {
        var response = await _client.GetAsync(MeUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-AUTH-006: access token không hợp lệ → 401")]
    public async Task GetMe_WithInvalidAccessToken_Returns401()
    {
        var response = await GetMeAsync("not.a.valid.jwt");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-AUTH-006: access token bị sửa chữ ký → 401")]
    public async Task GetMe_WithTamperedSignature_Returns401()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);
        var parts = login.AccessToken.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}";

        var response = await GetMeAsync(tampered);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-AUTH-006/A1,D48: user bị xóa sau khi token được cấp → 404 USER_NOT_FOUND")]
    public async Task GetMe_UserDeletedAfterTokenIssued_Returns404()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);
        await DeleteUserAsync(seeded.Id);

        var response = await GetMeAsync(login.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem!.Type.Should().Be(UserNotFound);
    }

    [Fact(DisplayName = "FR-AUTH-006/D48: user IsActive=false còn access token → 200 (chỉ đọc; D11 chặn ở login/refresh)")]
    public async Task GetMe_InactiveUserWithValidToken_Returns200()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);
        await DeactivateUserAsync(seeded.Id);

        var response = await GetMeAsync(login.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-AUTH-006/NFR-SEC-006: mỗi user chỉ thấy hồ sơ của chính mình")]
    public async Task GetMe_TwoUsers_EachSeesOwnProfile()
    {
        var first = await SeedUserAsync(displayName: "Người Một");
        var second = await SeedUserAsync(displayName: "Người Hai");
        var firstLogin = await LoginAsync(first.Email);
        var secondLogin = await LoginAsync(second.Email);

        var firstProfile = await (await GetMeAsync(firstLogin.AccessToken)).Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        var secondProfile = await (await GetMeAsync(secondLogin.AccessToken)).Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);

        firstProfile!.Id.Should().Be(first.Id);
        firstProfile.DisplayName.Should().Be("Người Một");
        secondProfile!.Id.Should().Be(second.Id);
        secondProfile.DisplayName.Should().Be("Người Hai");
    }

    /// <summary>HttpRequestMessage riêng cho từng lần gọi để không dính header Authorization giữa các user.</summary>
    private async Task<HttpResponseMessage> GetMeAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<(string Id, string Email)> SeedUserAsync(
        string displayName = "Người dùng test",
        string? avatarUrl = null,
        string? bio = null,
        string role = "Author")
    {
        var email = $"profile-{Guid.NewGuid():N}@example.com";

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            Bio = bio,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Tạo user test thất bại: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, role);
        return (user.Id, email);
    }

    private async Task<AuthResponseDto> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions))!;
    }

    private async Task DeleteUserAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        (await userManager.DeleteAsync(user!)).Succeeded.Should().BeTrue();
    }

    private async Task DeactivateUserAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        user!.IsActive = false;
        (await userManager.UpdateAsync(user)).Succeeded.Should().BeTrue();
    }
}
