using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using Microsoft.AspNetCore.Identity;
namespace CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Identity;
/// <summary>
/// Hiện thực IIdentityService bằng ASP.NET Core Identity (D23/ADR-0003).
/// Đây là ranh giới DUY NHẤT giữa tầng trên và <see cref="ApplicationUser"/>/<see cref="UserManager{TUser}"/>.
/// </summary>
public sealed class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService
{
    
        internal static class Roles
{
    public const string Admin = "Admin";
    public const string Author = "Author";
    public const string User = "User";
}
        public async Task<AuthenticatedUser> CreateUserAsync(
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
           throw new Exception("Lỗi đăng ký tài khoản");        }

        await userManager.AddToRoleAsync(user, Roles.Author);

        return new AuthenticatedUser(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, [Roles.Author]);
    }

    public async Task<AuthenticatedUser> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        // SRS FR-AUTH-002 A1 — email không tồn tại hoặc sai mật khẩu đều trả cùng một lỗi
        // chung, không tiết lộ tài khoản có tồn tại hay không (chống User Enumeration).
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
        {
            if (user is not null)
            {
                // SRS A3: đếm lần sai để Identity tự khóa sau 5 lần (D17 — LockoutOptions).
                await userManager.AccessFailedAsync(user);
            }

            throw new UnauthorizedException(ErrorCodes.AuthInvalidCredentials, "Email hoặc mật khẩu không đúng.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
            var remaining = lockoutEnd.HasValue ? lockoutEnd.Value - DateTimeOffset.UtcNow : TimeSpan.Zero;
            var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));

            throw new LockedException(
                ErrorCodes.AuthAccountLocked,
                $"Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau khoảng {minutes} phút.");
        }

        if (!user.IsActive)
        {
            // D11 — tài khoản bị Admin vô hiệu hóa (thao tác trực tiếp trên DB, ngoài scope v1).
            throw new ForbiddenException(ErrorCodes.AuthAccountDisabled, "Tài khoản đã bị vô hiệu hóa.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);

        return new AuthenticatedUser(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, [.. roles]);
    }
}
