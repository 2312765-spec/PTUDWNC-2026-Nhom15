using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Auth.Queries.GetCurrentUser;

/// <summary>
/// FR-AUTH-006 (D5, D12, D48) — hồ sơ của chính user đang đăng nhập. User đã bị xóa → 404
/// USER_NOT_FOUND; user IsActive=false vẫn đọc được. Không cache (dữ liệu theo từng user).
/// </summary>
public sealed class GetCurrentUserQueryHandler(
    ICurrentUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<GetCurrentUserQuery, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException(ErrorCodes.AuthTokenInvalid, "Access token không hợp lệ.");

        var user = await identityService.GetUserByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.UserNotFound, "Không tìm thấy người dùng.");

        return new UserProfileDto(user.UserId, user.Email, user.DisplayName, user.AvatarUrl, user.Bio, user.Roles);
    }
}
