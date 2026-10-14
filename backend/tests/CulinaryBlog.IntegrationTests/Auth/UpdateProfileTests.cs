using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
/// FR-AUTH-007 — SRS Chương 3 (cập nhật hồ sơ cá nhân) + docs/decisions.md D5, D12, D4, D48 và D54:
/// PATCH /auth/me nhận { displayName?, avatarUrl?, bio? }; lỗi dữ liệu → 400 VALIDATION_ERROR (không 422);
/// vắng/null = giữ nguyên, "" = xóa; user bị xóa → 404 USER_NOT_FOUND; IsActive=false → 200.
/// Xem docs/plans/FR-AUTH-007-cap-nhat-ho-so-ca-nhan.md.
/// </summary>
public sealed class UpdateProfileTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string Password = "Str0ng!Pass1";
    private const string MeUrl = "/api/v1/auth/me";
    private const string ValidationError = "VALIDATION_ERROR";
    private const string UserNotFound = "USER_NOT_FOUND";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] _d5Fields = ["id", "email", "displayName", "avatarUrl", "bio", "roles"];

    private static readonly string[] _forbiddenFields =
    [
        "fullName", "userName", "emailConfirmed", "createdAt",
        "passwordHash", "securityStamp", "normalizedEmail", "normalizedUserName", "concurrencyStamp"
    ];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-AUTH-007: đổi displayName → 200 và GET /me thấy giá trị mới")]
    public async Task Patch_DisplayName_Returns200AndPersists()
    {
        var seeded = await SeedUserAsync(displayName: "Tên Cũ");
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { displayName = "Tên Mới" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        updated!.DisplayName.Should().Be("Tên Mới");

        var me = await (await GetMeAsync(login.AccessToken)).Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        me!.DisplayName.Should().Be("Tên Mới");
    }

    [Fact(DisplayName = "FR-AUTH-007: cập nhật cả 3 field displayName/avatarUrl/bio")]
    public async Task Patch_AllFields_UpdatesAll()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new
        {
            displayName = "Bếp Trưởng",
            avatarUrl = "https://example.com/avatar.png",
            bio = "Yêu ẩm thực Việt"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        updated!.Id.Should().Be(seeded.Id);
        updated.Email.Should().Be(seeded.Email);
        updated.DisplayName.Should().Be("Bếp Trưởng");
        updated.AvatarUrl.Should().Be("https://example.com/avatar.png");
        updated.Bio.Should().Be("Yêu ẩm thực Việt");
    }

    [Fact(DisplayName = "FR-AUTH-007: PATCH một phần — chỉ gửi bio thì displayName/avatarUrl giữ nguyên")]
    public async Task Patch_OnlyBio_KeepsOtherFields()
    {
        var seeded = await SeedUserAsync(displayName: "Giữ Nguyên", avatarUrl: "https://example.com/keep.png");
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { bio = "Bio mới" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        updated!.DisplayName.Should().Be("Giữ Nguyên");
        updated.AvatarUrl.Should().Be("https://example.com/keep.png");
        updated.Bio.Should().Be("Bio mới");
    }

    [Fact(DisplayName = "FR-AUTH-007/D54-2: field null → giữ nguyên giá trị cũ")]
    public async Task Patch_NullField_KeepsExistingValue()
    {
        var seeded = await SeedUserAsync(displayName: "Tên Gốc", bio: "Bio gốc");
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, """{ "displayName": "Tên Khác", "bio": null }""");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        updated!.DisplayName.Should().Be("Tên Khác");
        updated.Bio.Should().Be("Bio gốc");
    }

    [Fact(DisplayName = "FR-AUTH-007/D54-2: chuỗi rỗng xóa bio và avatarUrl → null")]
    public async Task Patch_EmptyString_ClearsBioAndAvatar()
    {
        var seeded = await SeedUserAsync(avatarUrl: "https://example.com/a.png", bio: "Sẽ bị xóa");
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { avatarUrl = "", bio = "" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.GetProperty("bio").ValueKind.Should().Be(JsonValueKind.Null);

        var me = await (await GetMeAsync(login.AccessToken)).Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        me!.AvatarUrl.Should().BeNull();
        me.Bio.Should().BeNull();
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: response có đúng 6 trường, không fullName/userName/emailConfirmed/passwordHash (D12)")]
    public async Task Patch_ResponseContainsExactlyD5Fields_NoForbiddenFields()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { displayName = "Kiểm Tra Trường" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        names.Should().BeEquivalentTo(_d5Fields);

        var lowered = names.Select(n => n.ToLowerInvariant()).ToHashSet();
        foreach (var field in _forbiddenFields)
        {
            lowered.Should().NotContain(field.ToLowerInvariant(), $"D5/D12 loại '{field}' khỏi response");
        }

        body.Should().NotContain("AQAAAA", because: "không bao giờ trả hash mật khẩu");
    }

    [Fact(DisplayName = "FR-AUTH-007/D54-6: email và userName trong body bị bỏ qua, không đổi được")]
    public async Task Patch_EmailAndUserNameInBody_AreIgnored()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new
        {
            displayName = "Vẫn Đổi Được Tên",
            email = "hacker@example.com",
            userName = "hacker",
            fullName = "Hacker"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        updated!.Email.Should().Be(seeded.Email);
        updated.DisplayName.Should().Be("Vẫn Đổi Được Tên");

        // Đăng nhập bằng email cũ vẫn được → UserName/Email trong DB không đổi.
        var relogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = seeded.Email, password = Password });
        relogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: cập nhật không đổi roles")]
    public async Task Patch_DoesNotChangeRoles()
    {
        var seeded = await SeedUserAsync(role: "Admin");
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { displayName = "Admin Mới", roles = new[] { "Author" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        updated!.Roles.Should().BeEquivalentTo(["Admin"]);
    }

    [Theory(DisplayName = "FR-AUTH-007/D4,D5: displayName ngoài 2–100 ký tự hoặc rỗng → 400 VALIDATION_ERROR (không 422)")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public async Task Patch_InvalidDisplayName_Returns400(string displayName)
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { displayName });

        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: displayName 101 ký tự → 400 VALIDATION_ERROR")]
    public async Task Patch_DisplayNameTooLong_Returns400()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { displayName = new string('a', 101) });

        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: displayName đúng 100 ký tự → 200 (biên trên)")]
    public async Task Patch_DisplayNameExactly100_Returns200()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { displayName = new string('a', 100) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "FR-AUTH-007/D5,D54-3: avatarUrl không phải URL http/https hợp lệ → 400 VALIDATION_ERROR")]
    [InlineData("not-a-url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("/relative/path.png")]
    public async Task Patch_InvalidAvatarUrl_Returns400(string avatarUrl)
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { avatarUrl });

        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: bio 1001 ký tự → 400 VALIDATION_ERROR")]
    public async Task Patch_BioTooLong_Returns400()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { bio = new string('b', 1001) });

        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: bio đúng 1000 ký tự → 200 (biên trên)")]
    public async Task Patch_BioExactly1000_Returns200()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { bio = new string('b', 1000) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "FR-AUTH-007/D54-1: body không có field nào → 400 VALIDATION_ERROR")]
    [InlineData("{}")]
    [InlineData("""{ "displayName": null, "avatarUrl": null, "bio": null }""")]
    public async Task Patch_EmptyBody_Returns400(string json)
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, json);

        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "FR-AUTH-007/D4: dữ liệu sai không làm thay đổi hồ sơ")]
    public async Task Patch_InvalidData_DoesNotModifyProfile()
    {
        var seeded = await SeedUserAsync(displayName: "Không Đổi", bio: "Bio cũ");
        var login = await LoginAsync(seeded.Email);

        var response = await PatchMeAsync(login.AccessToken, new { displayName = "Tên hợp lệ", bio = new string('b', 1001) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var me = await (await GetMeAsync(login.AccessToken)).Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        me!.DisplayName.Should().Be("Không Đổi");
        me.Bio.Should().Be("Bio cũ");
    }

    [Fact(DisplayName = "FR-AUTH-007/NFR-SEC-006: không có Authorization header → 401")]
    public async Task Patch_WithoutAuthorizationHeader_Returns401()
    {
        var response = await _client.PatchAsJsonAsync(MeUrl, new { displayName = "Ẩn Danh" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-AUTH-007: access token không hợp lệ → 401")]
    public async Task Patch_WithInvalidAccessToken_Returns401()
    {
        var response = await PatchMeAsync("not.a.valid.jwt", new { displayName = "Token Giả" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-AUTH-007/D48,D54-5: user bị xóa sau khi token được cấp → 404 USER_NOT_FOUND")]
    public async Task Patch_UserDeletedAfterTokenIssued_Returns404()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);
        await DeleteUserAsync(seeded.Id);

        var response = await PatchMeAsync(login.AccessToken, new { displayName = "Đã Bị Xóa" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem!.Type.Should().Be(UserNotFound);
    }

    [Fact(DisplayName = "FR-AUTH-007/D54-4: user IsActive=false còn access token → 200")]
    public async Task Patch_InactiveUserWithValidToken_Returns200()
    {
        var seeded = await SeedUserAsync();
        var login = await LoginAsync(seeded.Email);
        await DeactivateUserAsync(seeded.Id);

        var response = await PatchMeAsync(login.AccessToken, new { displayName = "Bị Vô Hiệu" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-AUTH-007/NFR-SEC-006: user A cập nhật hồ sơ không ảnh hưởng user B")]
    public async Task Patch_TwoUsers_OnlyOwnProfileChanges()
    {
        var first = await SeedUserAsync(displayName: "Người Một");
        var second = await SeedUserAsync(displayName: "Người Hai");
        var firstLogin = await LoginAsync(first.Email);
        var secondLogin = await LoginAsync(second.Email);

        var response = await PatchMeAsync(firstLogin.AccessToken, new { displayName = "Người Một Đã Đổi" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondProfile = await (await GetMeAsync(secondLogin.AccessToken)).Content.ReadFromJsonAsync<UserProfileDto>(_jsonOptions);
        secondProfile!.Id.Should().Be(second.Id);
        secondProfile.DisplayName.Should().Be("Người Hai");
    }

    [Fact(DisplayName = "FR-AUTH-007: đăng nhập lại sau khi cập nhật trả hồ sơ mới")]
    public async Task Patch_ThenLogin_ReturnsUpdatedProfile()
    {
        var seeded = await SeedUserAsync(displayName: "Trước Khi Đổi");
        var login = await LoginAsync(seeded.Email);
        (await PatchMeAsync(login.AccessToken, new { displayName = "Sau Khi Đổi" })).EnsureSuccessStatusCode();

        var relogin = await LoginAsync(seeded.Email);

        relogin.User.DisplayName.Should().Be("Sau Khi Đổi");
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "D4: validation fail → 400, không dùng 422");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem!.Type.Should().Be(ValidationError);
    }

    private async Task<HttpResponseMessage> PatchMeAsync(string accessToken, object body) =>
        await PatchMeAsync(accessToken, JsonSerializer.Serialize(body, _jsonOptions));

    /// <summary>HttpRequestMessage riêng cho từng lần gọi để không dính header Authorization giữa các user.</summary>
    private async Task<HttpResponseMessage> PatchMeAsync(string accessToken, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, MeUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

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
        var email = $"update-profile-{Guid.NewGuid():N}@example.com";

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
