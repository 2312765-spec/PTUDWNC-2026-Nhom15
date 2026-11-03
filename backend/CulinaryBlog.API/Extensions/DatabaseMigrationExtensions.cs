using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.API.Extensions;

/// <summary>
/// CONS-009/D59 — dữ liệu hệ thống bắt buộc ở MỌI môi trường có DB thật: schema (EF Core migration) và
/// 2 role (<see cref="IdentityRoleSeeder"/>). Production gọi qua <c>--migrate-only</c> (service
/// <c>migrate</c>), Development gọi lúc khởi động rồi chạy thêm DbSeeder. Idempotent.
/// </summary>
public static class DatabaseMigrationExtensions
{
    public const string MigrateOnlyArgument = "--migrate-only";

    public static async Task MigrateAndSeedRolesAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        await IdentityRoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }
}
