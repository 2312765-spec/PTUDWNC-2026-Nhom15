namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>HỢP ĐỒNG CHUNG — chủ sở hữu: A. CONS-004, NFR-SEC-002.</summary>
public interface IJwtService
{
    /// <summary>
    /// Access token HS256, TTL 15 phút. Claims: userId, email, roles, jti.
    /// Trả kèm ExpiresAt (thay vì để caller tự tính lại TTL từ config) để tránh lệch giờ
    /// giữa claim "exp" trong JWT và AuthResponseDto.ExpiresAt trả cho client (D24).
    /// </summary>
    (string Token, DateTime ExpiresAt) GenerateAccessToken(string userId, string email, IEnumerable<string> roles);

    /// <summary>
    /// Refresh token 128-bit random (D25). Trả về (raw cho client, SHA-256 hash để lưu DB,
    /// ExpiresAt) — D20.
    /// </summary>
    (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken();

    /// <summary>Hash một raw token để tra cứu trong bảng RefreshTokens.</summary>
    string HashToken(string rawToken);
}
