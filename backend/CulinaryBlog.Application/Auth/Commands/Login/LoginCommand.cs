using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.Login;

/// <summary>FR-AUTH-002. D5: body { email, password }.</summary>
public sealed record LoginCommand(
    string Email,
    string Password,
    string? IpAddress) : IRequest<AuthResponseDto>;
