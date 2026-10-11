using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Auth.Commands.Refresh;

/// <summary>
/// FR-AUTH-004 — Token Rotation + Reuse Detection (NFR-SEC-002; D11, D20, D24, D25, D35).
/// Thứ tự kiểm tra theo D35-4: tồn tại → revoked → expired → user.
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IIdentityService identityService,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = jwtService.HashToken(request.RefreshToken);
        var existing = await refreshTokenRepository.GetByTokenAsync(tokenHash, cancellationToken)
            ?? throw new UnauthorizedException(ErrorCodes.AuthTokenInvalid, "Refresh token không hợp lệ.");

        if (existing.IsRevoked)
        {
            // SRS A3 — reuse attack. Không log raw token/hash, chỉ định danh để điều tra.
            var revokedCount = await refreshTokenRepository.RevokeAllActiveForUserAsync(existing.UserId, cancellationToken);
            logger.LogWarning(
                "SECURITY ALERT: refresh token đã bị revoke được dùng lại (reuse). UserId {UserId}, TokenId {TokenId}, IP {IpAddress}. Đã revoke {RevokedCount} refresh token còn hiệu lực của user",
                existing.UserId,
                existing.Id,
                request.IpAddress,
                revokedCount);

            throw RevokedException();
        }

        if (existing.IsExpired)
        {
            throw new UnauthorizedException(ErrorCodes.AuthRefreshTokenExpired, "Refresh token đã hết hạn. Vui lòng đăng nhập lại.");
        }

        // A4 — user bị xóa → null (401); IsActive == false → IIdentityService ném 403 (D11).
        var user = await identityService.GetUserForRefreshAsync(existing.UserId, cancellationToken)
            ?? throw new UnauthorizedException(ErrorCodes.AuthTokenInvalid, "Refresh token không hợp lệ.");

        var (accessToken, accessExpiresAt) = jwtService.GenerateAccessToken(user.UserId, user.Email, user.Roles);
        var (rawRefreshToken, newTokenHash, refreshExpiresAt) = jwtService.GenerateRefreshToken();

        // D35-6 — request đồng thời khác đã rotation token này trước: từ chối, KHÔNG revoke family.
        if (!await refreshTokenRepository.TryRevokeAsync(tokenHash, newTokenHash, cancellationToken))
        {
            throw RevokedException();
        }

        var newToken = RefreshToken.Create(user.UserId, newTokenHash, refreshExpiresAt, request.IpAddress);
        await refreshTokenRepository.AddAsync(newToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var profile = new UserProfileDto(user.UserId, user.Email, user.DisplayName, user.AvatarUrl, user.Bio, user.Roles);

        return new AuthResponseDto(accessToken, rawRefreshToken, accessExpiresAt, profile);
    }

    private static UnauthorizedException RevokedException()
        => new(ErrorCodes.AuthRefreshTokenRevoked, "Refresh token đã bị thu hồi. Vui lòng đăng nhập lại.");
}
