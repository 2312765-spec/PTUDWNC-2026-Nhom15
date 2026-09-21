using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Auth.Commands.Register;
using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Module Xác thực — SRS mục 8.1. Chủ sở hữu: <b>A</b>. Slice S2, S9.
///
/// Quyết định bắt buộc: D5 (displayName/avatarUrl/bio — KHÔNG có fullName/userName),
/// D20 (RefreshToken schema), D4 (validation → 400), D9 (Google: body { idToken }),
/// D11 (IsActive → 403), D17 (lockout → 423), D24 (register trả AuthResponseDto đầy đủ).
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
             .WithSummary("Đăng ký tài khoản mới — body { email, password, displayName } (D5)")
             .Produces<AuthResponseDto>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status409Conflict);

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

    /// <summary>
    /// CONS-008: endpoint chỉ nhận request → gửi command → trả kết quả. Không business logic,
    /// không validate ở đây (ValidationBehavior lo việc đó).
    /// </summary>
    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken ct)
    {
        var command = new RegisterCommand(
            request.Email,
            request.Password,
            request.DisplayName,
            httpContext.Connection.RemoteIpAddress?.ToString());

        var result = await sender.Send(command, ct);

        return TypedResults.Created((string?)null, result);
    }
}

/// <summary>D5 — wire contract của POST /auth/register.</summary>
public sealed record RegisterRequest(string Email, string Password, string DisplayName);
