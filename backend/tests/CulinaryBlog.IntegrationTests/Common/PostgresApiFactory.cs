using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
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
///
/// Redis cũng dùng Testcontainers riêng cho mỗi factory (D8). Trước đây trỏ "localhost:6379":
/// trên máy dev đó là Redis của docker compose, dùng CHUNG giữa mọi lớp test — cache
/// "categories:all" của lớp này lọt sang lớp khác có DB rỗng (GetCategoriesTests fail dù code
/// đúng). Trên CI thì không có Redis nên cache bị bỏ qua hoàn toàn, CachingBehavior và
/// invalidation chưa từng được test.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("culinaryblog_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync().AsTask();
        await _redis.DisposeAsync().AsTask();
        await base.DisposeAsync().AsTask();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:Port", "1");

        // FR-AUTH-003: không thể tạo ID Token Google hợp lệ trong test — thay IGoogleTokenValidator
        // thật bằng FakeGoogleTokenValidator (xem GoogleLoginTests).
        builder.ConfigureTestServices(services =>
            services.AddScoped<IGoogleTokenValidator, FakeGoogleTokenValidator>());
    }
}