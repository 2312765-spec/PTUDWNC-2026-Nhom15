using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.Refresh;

/// <summary>FR-AUTH-004. Body { refreshToken } — raw token client đang giữ (D20: server tự hash).</summary>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress) : IRequest<AuthResponseDto>;
