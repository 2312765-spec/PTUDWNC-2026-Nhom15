using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.Logout;

/// <summary>FR-AUTH-005. Body { refreshToken } — raw token client đang giữ (D20: server tự hash).</summary>
public sealed record LogoutCommand(string RefreshToken) : IRequest;
