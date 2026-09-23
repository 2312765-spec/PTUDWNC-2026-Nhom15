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
    Task<AuthenticatedUser> CreateUserAsync(
        string email,
        string password,
        string displayName,
        CancellationToken ct = default);

    /// <summary>
    /// FR-AUTH-002 — xác thực email/password. Thứ tự kiểm tra theo SRS Chương 3 bước 5-6 + D11:
    /// email/password sai → UnauthorizedException (AUTH_INVALID_CREDENTIALS, 401, thông điệp
    /// chung — chống User Enumeration). Tài khoản khóa → LockedException (AUTH_ACCOUNT_LOCKED,
    /// 423, D17). IsActive == false → ForbiddenException (AUTH_ACCOUNT_DISABLED, 403, D11).
    /// </summary>
    Task<AuthenticatedUser> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default);

    /// <summary>
    /// FR-AUTH-003, D9 — sau khi <c>IGoogleTokenValidator</c> đã verify ID Token. Đã có user với
    /// email đó (đăng ký thủ công trước) → liên kết (AddLoginAsync), <c>IsNewUser=false</c>.
    /// Chưa có → tạo mới, gán role "Author", <c>displayName</c>/<c>avatarUrl</c> lấy từ Google
    /// profile, <c>IsNewUser=true</c>. Không bao giờ tạo tài khoản thứ hai cho cùng một email.
    /// </summary>
    Task<(AuthenticatedUser User, bool IsNewUser)> LoginOrRegisterWithGoogleAsync(
        string email,
        string displayName,
        string? avatarUrl,
        string providerKey,
        CancellationToken ct = default);
}

/// <summary>Hồ sơ user sau khi tạo/đăng nhập thành công — Application không cần biết gì thêm về ApplicationUser.</summary>
public sealed record AuthenticatedUser(
    string UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    IReadOnlyList<string> Roles);
