using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.Register;

/// <summary>
/// FR-AUTH-001, luồng chính (SRS Chương 3, bước 4-12; D5, D20, D23, D24, D25).
/// Auto-login sau đăng ký: tạo user → gán role Author → sinh access+refresh token →
/// lưu refresh token → enqueue welcome email (fire-and-forget) → trả AuthResponseDto.
/// </summary>
public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IBackgroundJobService backgroundJobService)
    : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var user = await identityService.CreateUserAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            cancellationToken);

        var (accessToken, accessExpiresAt) = jwtService.GenerateAccessToken(user.UserId, user.Email, user.Roles);
        var (rawRefreshToken, refreshTokenHash, refreshExpiresAt) = jwtService.GenerateRefreshToken();

        var refreshToken = RefreshToken.Create(user.UserId, refreshTokenHash, refreshExpiresAt, request.IpAddress);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        backgroundJobService.EnqueueWelcomeEmail(user.Email, user.DisplayName);

        var profile = new UserProfileDto(user.UserId, user.Email, user.DisplayName, user.AvatarUrl, user.Bio, user.Roles);

        return new AuthResponseDto(accessToken, rawRefreshToken, accessExpiresAt, profile);
    }
}
