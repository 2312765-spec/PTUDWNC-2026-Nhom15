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
using Xunit;

namespace CulinaryBlog.IntegrationTests.Recipes.Support;

/// <summary>
/// Factory riêng cho ImagesTests (FR-RCP-008/FR-FILE-001/002) — giống <c>PostgresApiFactory</c>
/// (Postgres thật qua Testcontainers) nhưng thay <see cref="IFileStorageService"/> thật (MinIO)
/// bằng <see cref="FakeFileStorageService"/>, vì test này chỉ nhắm vào Command/Handler/Domain/
/// endpoint — không nhắm vào việc gọi S3 API thật. Không dùng chung <c>PostgresApiFactory</c> vì
/// lớp đó là <c>sealed</c> và không có điểm mở để thay service riêng cho một nhóm test.
/// </summary>
public sealed class RecipeImagesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("culinaryblog_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public FakeFileStorageService FileStorage { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Truy cập Services buộc host build ngay bây giờ (ConfigureWebHost đọc
        // _postgres.GetConnectionString(), nên container phải start TRƯỚC dòng này).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        await db.Database.MigrateAsync();

        // Program.cs chỉ seed role trong nhánh IsDevelopment() — factory chạy ở "Testing"
        // nên phải tự seed (xem bug đã ghi lại ở PostgresApiFactory/LoginTests).
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

        // UseSetting (không phải ConfigureAppConfiguration) — xem lý do chi tiết ở
        // PostgresApiFactory: Program.cs đọc builder.Configuration[...] đồng bộ trước Build().
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
        builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
        builder.UseSetting("Jwt:Issuer", "CulinaryBlog");
        builder.UseSetting("Jwt:Audience", "CulinaryBlogClient");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:Port", "1");

        builder.ConfigureTestServices(services =>
            services.AddScoped<IFileStorageService>(_ => FileStorage));
    }
}
