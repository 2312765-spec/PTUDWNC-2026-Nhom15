using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.GoogleLogin;

/// <summary>
/// FR-AUTH-003 (SRS Chương 3; D9, D5, D12, D33). Verify ID Token với Google → liên kết tài
/// khoản đã có hoặc tự tạo mới (role Author) → sinh cặp access+refresh token (giống hệt
/// Register/Login, D24) → nếu là tài khoản mới thì enqueue welcome email (FR-JOB-001).
/// </summary>
public sealed class GoogleLoginCommandHandler(
    IGoogleTokenValidator googleTokenValidator,
    IIdentityService identityService,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IBackgroundJobService backgroundJobService)
    : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var googleUser = await googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);

        var (user, isNewUser) = await identityService.LoginOrRegisterWithGoogleAsync(
            googleUser.Email,
            googleUser.Name,
            googleUser.Picture,
            googleUser.ProviderKey,
            cancellationToken);

        var (accessToken, accessExpiresAt) = jwtService.GenerateAccessToken(user.UserId, user.Email, user.Roles);
        var (rawRefreshToken, refreshTokenHash, refreshExpiresAt) = jwtService.GenerateRefreshToken();

        var refreshToken = RefreshToken.Create(user.UserId, refreshTokenHash, refreshExpiresAt, request.IpAddress);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (isNewUser)
        {
            backgroundJobService.EnqueueWelcomeEmail(user.Email, user.DisplayName);
        }

        var profile = new UserProfileDto(user.UserId, user.Email, user.DisplayName, user.AvatarUrl, user.Bio, user.Roles);

        return new AuthResponseDto(accessToken, rawRefreshToken, accessExpiresAt, profile);
    }
}
