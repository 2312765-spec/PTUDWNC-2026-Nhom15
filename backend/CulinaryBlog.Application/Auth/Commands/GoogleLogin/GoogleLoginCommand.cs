using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.GoogleLogin;

/// <summary>FR-AUTH-003. D9: body { idToken }.</summary>
public sealed record GoogleLoginCommand(
    string IdToken,
    string? IpAddress) : IRequest<AuthResponseDto>;
