using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Resilience.Support;

/// <summary>
/// NFR-REL-002 — API với Postgres + Redis thật, cho phép TẮT và BẬT LẠI Redis giữa chừng.
///
/// Redis gắn vào một cổng host cố định (không để Docker chọn ngẫu nhiên): dừng rồi chạy lại
/// container thì Docker cấp cổng mới, API sẽ không bao giờ kết nối lại được — không giống sự cố
/// thật (Redis restart cùng địa chỉ).
/// </summary>
public sealed class RedisChaosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly int _redisPort = GetFreeTcpPort();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("culinaryblog_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis;

    public RedisChaosApiFactory()
    {
        _redis = new RedisBuilder().WithPortBinding(_redisPort, RedisBuilder.RedisPort).Build();
    }

    public bool RedisRunning { get; private set; }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        RedisRunning = true;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        await db.Database.MigrateAsync();
        await IdentityRoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync().AsTask();
        await _redis.DisposeAsync().AsTask();
        await base.DisposeAsync().AsTask();
    }

    /// <summary>
    /// Ghi dữ liệu Redis xuống đĩa (SAVE) rồi dừng container — khi chạy lại, cache cũ còn nguyên,
    /// giống Redis có persistence restart. Không có SAVE thì kịch bản "cache cũ sau khi Redis lên
    /// lại" phụ thuộc cấu hình save mặc định của image.
    /// </summary>
    public async Task StopRedisAsync()
    {
        await _redis.ExecAsync(["redis-cli", "SAVE"]);
        await _redis.StopAsync();
        RedisRunning = false;
    }

    /// <summary>Chạy lại Redis cùng cổng và chờ multiplexer của API kết nối lại.</summary>
    public async Task StartRedisAsync()
    {
        await _redis.StartAsync();
        RedisRunning = true;

        var multiplexer = Services.GetRequiredService<IConnectionMultiplexer>();
        var sw = Stopwatch.StartNew();
        while (!multiplexer.IsConnected)
        {
            if (sw.Elapsed > TimeSpan.FromSeconds(30))
                throw new TimeoutException("API không kết nối lại Redis sau 30 s.");
            await Task.Delay(100);
        }
    }

    /// <summary>Đọc Redis trực tiếp bằng kết nối riêng (không qua API).</summary>
    public async Task<bool> RedisKeyExistsAsync(string key)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync($"localhost:{_redisPort}");
        return await connection.GetDatabase().KeyExistsAsync(key);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", $"localhost:{_redisPort}");
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:Port", "1");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
