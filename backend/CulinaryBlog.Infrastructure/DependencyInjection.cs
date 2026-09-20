using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Caching;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CulinaryBlog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ---- PostgreSQL (CONS-006) ----
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Postgres trong cấu hình.");

        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<CulinaryBlogDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3));
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CulinaryBlogDbContext>());

        // ---- Redis (D8 — cache DUY NHẤT) ----
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Redis trong cấu hình.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false; // NFR-REL-002: Redis down vẫn khởi động được
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // TODO(S2 — A): IJwtService, IEmailService, ASP.NET Core Identity
        // TODO(S3 — B): ISlugHelper, CategoryRepository, RecipeRepository
        // TODO(S8 — D): IFileStorageService (MinioFileStorageService), Hangfire jobs

        return services;
    }
}
