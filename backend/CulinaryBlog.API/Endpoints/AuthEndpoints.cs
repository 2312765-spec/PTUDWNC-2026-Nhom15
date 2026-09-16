using CulinaryBlog.API.Extensions;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Module Xác thực — SRS mục 8.1. Chủ sở hữu: <b>A</b>. Slice S2, S9.
///
/// Quyết định bắt buộc: D5 (displayName/avatarUrl/bio — KHÔNG có fullName/userName),
/// D20 (RefreshToken schema), D4 (validation → 400), D9 (Google: body { idToken }),
/// D11 (IsActive → 403), D17 (lockout → 423).
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", () => NotImplementedResults.Pending("FR-AUTH-001", "A"))
             .WithSummary("Đăng ký tài khoản mới — body { email, password, displayName }");

        group.MapPost("/login", () => NotImplementedResults.Pending("FR-AUTH-002", "A"))
             .WithSummary("Đăng nhập email/password — body { email, password }");

        group.MapPost("/google", () => NotImplementedResults.Pending("FR-AUTH-003", "A"))
             .WithSummary("Đăng nhập Google — body { idToken } (D9)");

        group.MapPost("/refresh", () => NotImplementedResults.Pending("FR-AUTH-004", "A"))
             .WithSummary("Làm mới access token — rotation + reuse detection (D20)");

        group.MapPost("/logout", () => NotImplementedResults.Pending("FR-AUTH-005", "A"))
             .RequireAuthorization()
             .WithSummary("Đăng xuất — revoke refresh token, idempotent (204)");

        group.MapGet("/me", () => NotImplementedResults.Pending("FR-AUTH-006", "A"))
             .RequireAuthorization()
             .WithSummary("Hồ sơ người dùng hiện tại");

        group.MapPatch("/me", () => NotImplementedResults.Pending("FR-AUTH-007", "A"))
             .RequireAuthorization()
             .WithSummary("Cập nhật hồ sơ — body { displayName?, avatarUrl?, bio? }");
    }
}
