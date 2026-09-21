namespace CulinaryBlog.Application.Auth.Dtos;

/// <summary>
/// D24 — response chung cho mọi endpoint auto-login (register, login, refresh).
/// KHÔNG bao giờ chứa raw refresh token đã lưu DB dưới dạng hash — trường
/// <see cref="RefreshToken"/> ở đây là raw token trả cho CLIENT, khác với
/// RefreshToken.TokenHash lưu trong database (NFR-SEC-002).
/// </summary>
public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserProfileDto User);
