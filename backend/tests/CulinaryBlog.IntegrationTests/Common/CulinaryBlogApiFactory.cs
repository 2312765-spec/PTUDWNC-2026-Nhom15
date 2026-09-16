using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=culinaryblog_test;Username=postgres;Password=postgres",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["Jwt:Key"] = "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test",
                ["Jwt:Issuer"] = "CulinaryBlog",
                ["Jwt:Audience"] = "CulinaryBlogClient",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            });
        });
    }
}
