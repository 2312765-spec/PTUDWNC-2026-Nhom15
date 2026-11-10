using System.Buffers.Binary;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Security;

/// <summary>
/// NFR-SEC-001 (hash mật khẩu) và NFR-SEC-002 (access token) — kiểm trên dữ liệu thật sinh ra khi đăng ký,
/// không chỉ đọc cấu hình. Rotation / reuse / SHA-256 của refresh token đã có ở <c>Auth/RefreshTokenTests.cs</c>.
/// </summary>
public sealed class CredentialSecurityTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "NFR-SEC-001: PasswordHash trong DB là Identity V3 — PBKDF2-HMACSHA512, ≥ 100.000 vòng, salt 128-bit")]
    public async Task Register_StoresPbkdf2Sha512HashWithAtLeast100kIterations()
    {
        var (email, _) = await RegisterAsync();

        string? passwordHash;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            passwordHash = (await userManager.FindByEmailAsync(email))!.PasswordHash;
        }

        // Định dạng V3 của PasswordHasher<T>: [0x01][PRF uint32 BE][iter uint32 BE][saltLen uint32 BE][salt][subkey]
        var bytes = Convert.FromBase64String(passwordHash!);
        bytes[0].Should().Be(0x01, "định dạng V3 (PBKDF2), không phải V2 (HMACSHA1, 1.000 vòng)");
        BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)).Should().Be(
            (uint)KeyDerivationPrf.HMACSHA512, "NFR-SEC-001: PBKDF2-HMACSHA512");
        BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5)).Should().BeGreaterThanOrEqualTo(100_000u);
        BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(9)).Should().BeGreaterThanOrEqualTo(16u, "salt ≥ 128-bit");
    }

    [Fact(DisplayName = "NFR-SEC-002/CONS-004: access token ký HS256, sống 15 phút")]
    public async Task Register_IssuesHs256AccessTokenValidFor15Minutes()
    {
        var (_, auth) = await RegisterAsync();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);

        jwt.Header.Alg.Should().Be("HS256");
        // JwtService không ghi claim iat → đo exp so với thời điểm vừa phát hành.
        (jwt.ValidTo - DateTime.UtcNow).Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(1));
    }

    private async Task<(string Email, AuthResponseDto Auth)> RegisterAsync()
    {
        var email = $"sec-cred-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "Str0ng!Pass1", displayName = "Người test" });
        response.EnsureSuccessStatusCode();
        return (email, (await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions))!);
    }
}
