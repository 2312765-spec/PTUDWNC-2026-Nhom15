namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>HỢP ĐỒNG CHUNG — chủ sở hữu: A. CONS-004, NFR-SEC-002.</summary>
public interface IJwtService
{
    /// <summary>Access token HS256, TTL 15 phút. Claims: userId, email, roles, jti.</summary>
    string GenerateAccessToken(string userId, string email, IEnumerable<string> roles);

    /// <summary>Refresh token 128-bit random. Trả về (raw cho client, SHA-256 hash để lưu DB) — D20.</summary>
    (string RawToken, string TokenHash) GenerateRefreshToken();

    /// <summary>Hash một raw token để tra cứu trong bảng RefreshTokens.</summary>
    string HashToken(string rawToken);
}
