using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Auth.Commands.Logout;

/// <summary>
/// FR-AUTH-005 — thu hồi refresh token (D20, D35-6, D40). Idempotent: token không tồn tại, của user
/// khác, đã revoke hoặc hết hạn → no-op, endpoint vẫn trả 204 (không lộ trạng thái token).
/// KHÔNG kích hoạt reuse detection (D35-5 chỉ dành cho /refresh).
/// </summary>
public sealed class LogoutCommandHandler(
    ICurrentUser currentUser,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    ILogger<LogoutCommandHandler> logger)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = jwtService.HashToken(request.RefreshToken);
        var existing = await refreshTokenRepository.GetByTokenAsync(tokenHash, cancellationToken);

        if (existing is null || existing.IsRevoked || existing.IsExpired)
        {
            return;
        }

        // D40-2, NFR-SEC-006 — chỉ chủ sở hữu mới thu hồi được. Không lộ qua status code: vẫn 204.
        if (existing.UserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Logout với refresh token của user khác bị bỏ qua. UserId yêu cầu {RequestUserId}, TokenId {TokenId}",
                currentUser.UserId,
                existing.Id);
            return;
        }

        // D35-6 — UPDATE có điều kiện RevokedAt IS NULL; thua race thì kết quả vẫn là "đã revoke".
        await refreshTokenRepository.TryRevokeAsync(tokenHash, replacedByTokenHash: null, cancellationToken);
    }
}
