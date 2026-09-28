using CulinaryBlog.IntegrationTests.Common;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability.Support;

/// <summary>
/// FR-OBS-001 — dựng API với Postgres + một S3 server thật (Testcontainers) để "/health" tổng
/// hợp cả 3 component đều khỏe. Dùng <c>adobe/s3mock</c> thay MinIO — xem
/// <see cref="S3MockContainer"/> (MinIO đã khóa pull ẩn danh trên cả Docker Hub lẫn quay.io,
/// phát hiện qua CI đỏ của FR-FILE-001/002 cùng ngày). Redis dùng instance thật ở
/// localhost:6379 (docker-compose dev), giống quy ước đã có ở <c>PostgresApiFactory</c>.
/// </summary>
public sealed class HealthyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string BucketName = "culinary-blog";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("culinaryblog_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly IContainer _s3Server = S3MockContainer.Build(BucketName);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _s3Server.StartAsync());

        // Truy cập Services buộc host build ngay bây giờ — containers phải start TRƯỚC dòng này
        // (xem lý do chi tiết ở PostgresApiFactory).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        await db.Database.MigrateAsync();
        await IdentityRoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync().AsTask();
        await _s3Server.DisposeAsync().AsTask();
        await base.DisposeAsync().AsTask();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting (không phải ConfigureAppConfiguration) — xem lý do ở PostgresApiFactory.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:Port", "1");
        builder.UseSetting("Minio:Endpoint", _s3Server.GetConnectionString());
        builder.UseSetting("Minio:AccessKey", S3MockContainer.AccessKey);
        builder.UseSetting("Minio:SecretKey", S3MockContainer.SecretKey);
        builder.UseSetting("Minio:BucketName", BucketName);
    }
}
