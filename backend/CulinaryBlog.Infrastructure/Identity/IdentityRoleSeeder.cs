using CulinaryBlog.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Identity;

/// <summary>
/// Seed 2 role hệ thống ("Author", "Admin") — bắt buộc phải có để `IdentityService.CreateUserAsync`
/// gọi <c>UserManager.AddToRoleAsync</c> không ném lỗi.
///
/// Bug phát hiện lúc chạy CI thật (2026-09-22): `.AddRoles&lt;IdentityRole&gt;()` trong
/// DependencyInjection.cs chỉ đăng ký <see cref="RoleManager{TRole}"/>, KHÔNG tự tạo role
/// nào. `AddToRoleAsync` ném thẳng `InvalidOperationException("Role 'X' does not exist.")`
/// nếu role chưa tồn tại — không phải `IdentityResult.Failed` — nên rơi vào nhánh 500 mặc
/// định của GlobalExceptionMiddleware thay vì bị chặn từ sớm.
///
/// Role là dữ liệu hệ thống bắt buộc (khác dữ liệu mẫu của <c>DbSeeder</c>), nên PHẢI chạy
/// ở mọi nơi có Postgres thật: Program.cs (Development) và PostgresApiFactory (integration
/// test). Luôn gọi SAU migration — bảng AspNetRoles phải tồn tại trước.
/// </summary>
public static class IdentityRoleSeeder
{
    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { Roles.Author, Roles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
