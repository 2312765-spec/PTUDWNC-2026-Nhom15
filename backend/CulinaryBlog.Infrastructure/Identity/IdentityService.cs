using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Identity;

/// <summary>
/// Hiện thực IIdentityService bằng ASP.NET Core Identity (D23/ADR-0003).
/// Đây là ranh giới DUY NHẤT giữa tầng trên và <see cref="ApplicationUser"/>/<see cref="UserManager{TUser}"/>.
/// </summary>
public sealed class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService
{
    public async Task<CreatedUser> CreateUserAsync(
        string email,
        string password,
        string displayName,
        CancellationToken ct = default)
    {
        var user = new ApplicationUser
        {
            UserName = email, // D5: UserName = email, Identity yêu cầu có giá trị.
            Email = email,
            DisplayName = displayName,
        };

        var createResult = await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
            {
                throw new ConflictException(ErrorCodes.AuthEmailExists, "Email đã được đăng ký.");
            }

            // RegisterCommandValidator (FluentValidation) đã chặn password yếu từ trước —
            // nhánh này chỉ còn là phòng thủ chiều sâu (SRS FR-AUTH-001 A2, D4: 400).
            var detail = string.Join(" ", createResult.Errors.Select(e => e.Description));
            throw new DomainException(ErrorCodes.ValidationError, detail);
        }

        await userManager.AddToRoleAsync(user, Roles.Author);

        return new CreatedUser(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, [Roles.Author]);
    }
}
