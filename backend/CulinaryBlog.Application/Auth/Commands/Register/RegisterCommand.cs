using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.Register;

/// <summary>FR-AUTH-001. D5: body { email, password, displayName } — KHÔNG có fullName/userName.</summary>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string? IpAddress) : IRequest<AuthResponseDto>;
