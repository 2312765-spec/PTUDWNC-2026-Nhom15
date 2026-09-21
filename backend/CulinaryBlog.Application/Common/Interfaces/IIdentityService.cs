namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// HỢP ĐỒNG CHUNG — chủ sở hữu: A. D23/ADR-0003: ranh giới giữa Application và
/// ASP.NET Core Identity. Application không bao giờ biết kiểu ApplicationUser hay
/// UserManager&lt;T&gt; — chỉ nói chuyện qua interface này.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Tạo user mới, gán role "Author" mặc định (FR-AUTH-001).
    /// Ném ConflictException (AUTH_EMAIL_EXISTS, 409) nếu email đã tồn tại.
    /// Ném FluentValidation.ValidationException nếu Identity password policy fail (400, D4).
    /// </summary>
    Task<CreatedUser> CreateUserAsync(
        string email,
        string password,
        string displayName,
        CancellationToken ct = default);
}

/// <summary>Kết quả sau khi tạo user — Application không cần biết gì thêm về ApplicationUser.</summary>
public sealed record CreatedUser(
    string UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    IReadOnlyList<string> Roles);
