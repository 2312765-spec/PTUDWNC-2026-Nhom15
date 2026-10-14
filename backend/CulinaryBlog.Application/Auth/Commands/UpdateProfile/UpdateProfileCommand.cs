using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.Commands.UpdateProfile;

/// <summary>
/// FR-AUTH-007 (D5, D54) — PATCH một phần. Field null = giữ nguyên; chuỗi rỗng ở <c>AvatarUrl</c>/<c>Bio</c> = xóa.
/// UserId luôn lấy từ JWT qua ICurrentUser (NFR-SEC-006), không nhận từ body.
/// </summary>
public sealed record UpdateProfileCommand(string? DisplayName, string? AvatarUrl, string? Bio)
    : IRequest<UserProfileDto>;
