using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.Login;

/// <summary>
/// FR-AUTH-002, luồng chính (SRS Chương 3 bước 4-11; D5, D11, D17, D20, D24, D25).
/// Xác thực email/password → sinh cặp access+refresh token mới → lưu refresh token →
/// trả AuthResponseDto (giống hệt shape register, D24).
/// </summary>
public sealed class LoginCommandHandler(
    IIdentityService identityService,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await identityService.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);

        var (accessToken, accessExpiresAt) = jwtService.GenerateAccessToken(user.UserId, user.Email, user.Roles);
        var (rawRefreshToken, refreshTokenHash, refreshExpiresAt) = jwtService.GenerateRefreshToken();

        var refreshToken = RefreshToken.Create(user.UserId, refreshTokenHash, refreshExpiresAt, request.IpAddress);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var profile = new UserProfileDto(user.UserId, user.Email, user.DisplayName, user.AvatarUrl, user.Bio, user.Roles);

        return new AuthResponseDto(accessToken, rawRefreshToken, accessExpiresAt, profile);
    }
}
