using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CulinaryBlog.IntegrationTests.Observability.Support;

/// <summary>
/// FR-OBS-001 — API trỏ tới một Postgres không kết nối được (port 1, đóng ngay lập tức) để
/// "/health/ready" thật sự trả Unhealthy, không phải giả lập. Không dùng Testcontainers vì mục
/// đích là mô phỏng DB CHẾT, không phải chạy DB thật.
/// </summary>
public sealed class DegradedApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Port=1;Database=culinaryblog_test;Username=postgres;Password=postgres;Timeout=1");
        // Cổng 1 luôn đóng — không đụng Redis dev (localhost:6379); kết quả như nhau trên dev và CI.
        builder.UseSetting("ConnectionStrings:Redis", "localhost:1");
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:Port", "1");
    }
}
