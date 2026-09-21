namespace CulinaryBlog.Application.Auth.Dtos;

/// <summary>
/// D5 — shape chuẩn: { id, email, displayName, avatarUrl, bio, roles }.
/// KHÔNG có fullName/userName/emailConfirmed/createdAt (D5 loại khỏi response).
/// KHÔNG bao giờ chứa PasswordHash/SecurityStamp/raw refresh token.
/// </summary>
public sealed record UserProfileDto(
    string Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    IReadOnlyList<string> Roles);
