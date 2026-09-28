using Amazon.S3;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Observability.Support;

/// <summary>
/// FR-OBS-001 — dựng API với Postgres + MinIO thật (Testcontainers) để "/health" tổng hợp cả
/// 3 component đều khỏe. Redis dùng instance thật ở localhost:6379 (docker-compose dev), giống
/// quy ước đã có ở <c>PostgresApiFactory</c> — không dựng riêng Testcontainers.Redis cho việc này.
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

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("quay.io/minio/minio:latest")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());

        using var s3 = new AmazonS3Client(_minio.GetAccessKey(), _minio.GetSecretKey(), new AmazonS3Config
        {
            ServiceURL = _minio.GetConnectionString(),
            ForcePathStyle = true,
            UseHttp = true,
        });
        await s3.PutBucketAsync(BucketName);

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
        await _minio.DisposeAsync().AsTask();
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
        builder.UseSetting("Minio:Endpoint", _minio.GetConnectionString());
        builder.UseSetting("Minio:AccessKey", _minio.GetAccessKey());
        builder.UseSetting("Minio:SecretKey", _minio.GetSecretKey());
        builder.UseSetting("Minio:BucketName", BucketName);
    }
}
