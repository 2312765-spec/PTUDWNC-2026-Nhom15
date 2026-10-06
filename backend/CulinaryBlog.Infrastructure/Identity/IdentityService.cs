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
            // nhánh này chỉ còn là phòng thủ chiều sâu (SRS FR-AUTH-001 A2, D4: 400 chứ không
            // phải 500 — BadRequestException, không phải Exception trơn).
            var detail = string.Join(" ", createResult.Errors.Select(e => e.Description));
            throw new BadRequestException(ErrorCodes.ValidationError, detail);
        }

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

    public async Task<(AuthenticatedUser User, bool IsNewUser)> LoginOrRegisterWithGoogleAsync(
        string email,
        string displayName,
        string? avatarUrl,
        string providerKey,
        CancellationToken ct = default)
    {
        var loginInfo = new UserLoginInfo("Google", providerKey, "Google");

        // Tìm theo Google ID (sub) trước — bất biến, còn email trên Google có thể đổi. Chỉ khi chưa
        // liên kết mới tìm theo email (caller đã đảm bảo email_verified = true).
        var user = await userManager.FindByLoginAsync(loginInfo.LoginProvider, loginInfo.ProviderKey)
            ?? await userManager.FindByEmailAsync(email);

        if (user is not null)
        {
            // D9 — email đã đăng ký thủ công (hoặc Google) trước đó: LIÊN KẾT, không tạo
            // tài khoản thứ hai. displayName/avatarUrl của tài khoản hiện có giữ nguyên.
            var existingLogins = await userManager.GetLoginsAsync(user);
            if (existingLogins.All(l => l.LoginProvider != loginInfo.LoginProvider))
            {
                EnsureSucceeded(await userManager.AddLoginAsync(user, loginInfo), "liên kết Google");
            }

            var existingRoles = await userManager.GetRolesAsync(user);
            return (new AuthenticatedUser(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, [.. existingRoles]), false);
        }

        // D9, D5 — chưa có tài khoản: tự tạo, displayName/avatarUrl lấy từ Google profile,
        // role Author mặc định. Không có password (login duy nhất qua Google).
        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
        };

        var createResult = await userManager.CreateAsync(newUser);
        if (!createResult.Succeeded)
        {
            var detail = string.Join(" ", createResult.Errors.Select(e => e.Description));
            throw new BadRequestException(ErrorCodes.ValidationError, detail);
        }

        EnsureSucceeded(await userManager.AddToRoleAsync(newUser, Roles.Author), "gán role Author");
        EnsureSucceeded(await userManager.AddLoginAsync(newUser, loginInfo), "liên kết Google");

        return (new AuthenticatedUser(newUser.Id, newUser.Email!, newUser.DisplayName, newUser.AvatarUrl, newUser.Bio, [Roles.Author]), true);
    }

    // Lỗi Identity ở đây là lỗi hệ thống (không phải do input người dùng) → để middleware trả 500
    // và log đầy đủ, thay vì bỏ qua rồi vẫn phát token cho user ở trạng thái dở dang.
    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var detail = string.Join(" ", result.Errors.Select(e => e.Code));
            throw new InvalidOperationException($"Identity: {operation} thất bại ({detail}).");
        }
    }

    public async Task<AuthenticatedUser?> GetUserForRefreshAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        if (!user.IsActive)
        {
            // D11 — kiểm tra ở cả login và refresh.
            throw new ForbiddenException(ErrorCodes.AuthAccountDisabled, "Tài khoản đã bị vô hiệu hóa.");
        }

        // D35-3 — cố ý KHÔNG gọi IsLockedOutAsync: lockout chỉ chặn đoán mật khẩu ở login.
        var roles = await userManager.GetRolesAsync(user);

        return new AuthenticatedUser(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, [.. roles]);
    }
}
