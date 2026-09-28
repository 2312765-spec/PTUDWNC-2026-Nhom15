using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Auth.Commands.Login;
using CulinaryBlog.Application.Auth.Commands.Register;
using CulinaryBlog.Application.Auth.Dtos;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
             .WithSummary("Đăng ký tài khoản mới")
             .Produces<AuthResponseDto>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status409Conflict)
             .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
             .WithSummary("Đăng nhập email/password")
             .Produces<AuthResponseDto>(StatusCodes.Status200OK)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status423Locked)
             .AllowAnonymous();

        group.MapPost("/google", () => NotImplementedResults.Pending("FR-AUTH-003", "A"));
        group.MapPost("/refresh", () => NotImplementedResults.Pending("FR-AUTH-004", "A"));
        
        group.MapPost("/logout", () => NotImplementedResults.Pending("FR-AUTH-005", "A"))
             .RequireAuthorization();

        // NFR-SEC-006: Endpoint này test gọi khi chưa có token phải trả về 401
        group.MapGet("/me", () => NotImplementedResults.Pending("FR-AUTH-006", "A"))
             .RequireAuthorization();

        group.MapPatch("/me", () => NotImplementedResults.Pending("FR-AUTH-007", "A"))
             .RequireAuthorization();

        return app;
    }

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

        // Trả 201 kèm AuthResponseDto (auto-login theo D24)
        return TypedResults.Created("/api/v1/auth/me", result);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken ct)
    {
        var command = new LoginCommand(
            request.Email,
            request.Password,
            httpContext.Connection.RemoteIpAddress?.ToString());

        var result = await sender.Send(command, ct);

        return TypedResults.Ok(result);
    }
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);