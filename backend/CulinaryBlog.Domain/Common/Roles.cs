namespace CulinaryBlog.Domain.Common;

/// <summary>
/// SRS mục 2.3 — 3 vai trò. Guest không phải role, chỉ là "chưa đăng nhập".
/// Đặt ở Domain (không phải API) vì Infrastructure (IdentityService — FR-AUTH-001) và API
/// (policy, ICurrentUser) đều cần dùng, mà Infrastructure không được reference API.
/// </summary>
public static class Roles
{
    public const string Author = "Author";
    public const string Admin = "Admin";
}
