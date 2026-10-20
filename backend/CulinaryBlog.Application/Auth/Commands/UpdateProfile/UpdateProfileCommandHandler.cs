using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.UpdateProfile;

/// <summary>
/// FR-AUTH-007 (D5, D48, D54) — cập nhật hồ sơ của chính user đang đăng nhập. User đã bị xóa → 404
/// USER_NOT_FOUND; user IsActive=false vẫn cập nhật được. Không cache nên không cần invalidate.
/// </summary>
public sealed class UpdateProfileCommandHandler(
    ICurrentUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<UpdateProfileCommand, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException(ErrorCodes.AuthTokenInvalid, "Access token không hợp lệ.");

        var user = await identityService.UpdateProfileAsync(
                userId,
                request.DisplayName?.Trim(),
                request.AvatarUrl?.Trim(),
                request.Bio,
                cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.UserNotFound, "Không tìm thấy người dùng.");

        return new UserProfileDto(user.UserId, user.Email, user.DisplayName, user.AvatarUrl, user.Bio, user.Roles);
    }
}
