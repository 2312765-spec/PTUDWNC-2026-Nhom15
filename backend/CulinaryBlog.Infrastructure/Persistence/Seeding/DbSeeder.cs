using CulinaryBlog.Infrastructure.Persistence;

namespace CulinaryBlog.Infrastructure.Persistence.Seeding;

/// <summary>
/// DbSeeder tạm thời bỏ qua sinh dữ liệu mẫu để hệ thống build thành công.
/// Khi module Recipe hoàn thiện, thành viên phụ trách sẽ bổ sung dữ liệu seed sau.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(CulinaryBlogDbContext context)
    {
        await Task.CompletedTask;
    }
}