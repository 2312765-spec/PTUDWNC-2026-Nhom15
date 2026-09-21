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

        // UseSetting (không phải ConfigureAppConfiguration+AddInMemoryCollection) — với minimal
        // hosting Program.cs, ConfigureAppConfiguration không kịp áp dụng trước khi Program.cs
        // đọc builder.Configuration["Jwt:Key"], khiến AddAuthentication() không được gọi và
        // IAuthenticationSchemeProvider không resolve được. UseSetting ghi thẳng vào config
        // nguồn mà WebApplicationFactory dùng để khởi tạo builder, nên luôn có mặt kịp thời.
        builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Port=5432;Database=culinaryblog_test;Username=postgres;Password=postgres");
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
    }
}
