using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// Factory dựng API trong bộ nhớ cho integration test.
///
/// TODO(Sprint 1): implement IAsyncLifetime với Testcontainers.PostgreSql và
/// Testcontainers.Redis để mỗi lần chạy test có DB sạch. Xem docs/roadmap.md S2.
/// Hiện tại chỉ ghi đè connection string để API khởi động được mà không cần hạ tầng thật.
/// </summary>
public class CulinaryBlogApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // LƯU Ý: Program.cs (top-level, WebApplicationBuilder) đọc builder.Configuration[...]
        // ĐỒNG BỘ trước khi Build() (vd. connection string, Jwt:Key cho AddAuthentication).
        // ConfigureAppConfiguration(...) của WebApplicationFactory chỉ merge nguồn config MỚI
        // vào lúc Build() — QUÁ TRỄ cho các lần đọc đó, nên các giá trị test sẽ không tới nơi.
        // UseSetting(...) ghi thẳng vào webHostBuilder settings — nguồn được nạp SỚM, tới trước
        // khi Program.cs chạy code của nó. Dùng UseSetting cho mọi giá trị Program.cs cần sớm.
        builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Port=5432;Database=culinaryblog_test;Username=postgres;Password=postgres");
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
    }
}
