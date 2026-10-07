using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.Queries.GetCurrentUser;

/// <summary>FR-AUTH-006 — không tham số: UserId luôn lấy từ JWT qua ICurrentUser (NFR-SEC-006).</summary>
public sealed record GetCurrentUserQuery : IRequest<UserProfileDto>;
