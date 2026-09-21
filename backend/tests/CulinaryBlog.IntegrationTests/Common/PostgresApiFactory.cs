using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// Factory dựng API với PostgreSQL thật trong container (Testcontainers) — dùng cho các test
/// cần ghi/đọc DB thật (vd. FR-AUTH-001 register tạo ApplicationUser + RefreshToken).
/// SmokeTests dùng <see cref="CulinaryBlogApiFactory"/> (không cần DB) — factory này chỉ
/// dùng khi test thật sự cần Postgres.
///
/// LƯU Ý (xem CulinaryBlogApiFactory): Program.cs đọc builder.Configuration[...] ĐỒNG BỘ
/// trước Build(), nên override phải qua UseSetting(...), không phải ConfigureAppConfiguration.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("culinaryblog_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Truy cập Services buộc host build ngay bây giờ (ConfigureWebHost đọc _postgres.GetConnectionString(),
        // nên container phải start TRƯỚC dòng này).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        await db.Database.MigrateAsync();

        // Bug CI (2026-09-22): thiếu bước này thì mọi POST /auth/register trả 500 —
        // xem IdentityRoleSeeder. Program.cs chỉ seed role trong nhánh IsDevelopment(),
        // factory này chạy ở "Testing" nên phải tự seed sau khi migrate.
        await IdentityRoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync().AsTask();
        await base.DisposeAsync().AsTask();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:Port", "1");
    }
}
