using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.Infrastructure.Auth;

/// <summary>CONS-004, NFR-SEC-002. Access token HS256 15 phút, refresh token 128-bit (D25).</summary>
public sealed class JwtService(IConfiguration configuration) : IJwtService
{
    public (string Token, DateTime ExpiresAt) GenerateAccessToken(
        string userId,
        string email,
        IEnumerable<string> roles)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key.");
        var minutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);
        var expiresAt = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken()
    {
        var days = configuration.GetValue("Jwt:RefreshTokenDays", 7);
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)); // 128-bit (D25)

        return (rawToken, HashToken(rawToken), DateTime.UtcNow.AddDays(days));
    }

    public string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
