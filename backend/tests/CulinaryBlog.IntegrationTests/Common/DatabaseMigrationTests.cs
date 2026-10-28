using CulinaryBlog.API.Extensions;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// CONS-009/D59 — bước <c>--migrate-only</c> của service <c>migrate</c> trong docker-compose.prod.yml.
/// Khác <see cref="PostgresApiFactory"/>: factory ở đây KHÔNG tự migrate, DB bắt đầu trống hoàn toàn
/// như DB production lần đầu deploy.
/// </summary>
public sealed class DatabaseMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("culinaryblog_migrate_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private EmptyDatabaseApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new EmptyDatabaseApiFactory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact(DisplayName = "CONS-009/D59: MigrateAndSeedRolesAsync trên DB trống → đủ migration + 2 role, không có dữ liệu mẫu")]
    public async Task MigrateAndSeedRoles_EmptyDatabase_AppliesMigrationsAndSeedsRoles()
    {
        await _factory.Services.MigrateAndSeedRolesAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();

        var roles = await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
            .Roles.Select(r => r.Name).ToListAsync();
        roles.Should().BeEquivalentTo([Roles.Author, Roles.Admin]);

        // D59: prod KHÔNG chạy DbSeeder.
        (await db.Users.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "CONS-009/D59: chạy MigrateAndSeedRolesAsync 2 lần → idempotent, không nhân đôi role")]
    public async Task MigrateAndSeedRoles_RunTwice_IsIdempotent()
    {
        await _factory.Services.MigrateAndSeedRolesAsync();

        var act = () => _factory.Services.MigrateAndSeedRolesAsync();

        await act.Should().NotThrowAsync();
        using var scope = _factory.Services.CreateScope();
        var roleCount = await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().Roles.CountAsync();
        roleCount.Should().Be(2);
    }

    private sealed class EmptyDatabaseApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            // Cổng 1 luôn đóng — test không cần cache.
            builder.UseSetting("ConnectionStrings:Redis", "localhost:1");
            builder.UseSetting("Jwt:Key", "test-only-khoa-ky-jwt-toi-thieu-32-ky-tu-cho-integration-test");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        }
    }
}
