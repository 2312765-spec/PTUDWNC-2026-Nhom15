namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ truy xuất thông tin người dùng hiện tại từ ClaimsPrincipal / JWT Token
/// Phục vụ phân quyền lọc Draft / Published recipes (FR-CAT-002) và Admin (FR-CAT-003)
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin => Role == "Admin";
    bool IsAuthor => Role == "Author" || Role == "Admin";
}
