using Amazon.S3;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Caching;
using CulinaryBlog.Infrastructure.Email;
using CulinaryBlog.Infrastructure.Files;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
namespace CulinaryBlog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Đọc chuỗi kết nối từ DefaultConnection hoặc Postgres, có fallback mặc định
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? configuration.GetConnectionString("Postgres")
                            ?? "Host=localhost;Port=5432;Database=culinaryblog;Username=postgres;Password=postgres";

        // KHÔNG tắt PendingModelChangesWarning: nó là chốt chặn duy nhất khi code entity/config lệch
        // khỏi migration — lần trước bị tắt, model trôi xa DB đến mức migration tự sinh sẽ DROP 11 cột.
        services.AddDbContext<CulinaryBlogDbContext>(options =>
            options.UseNpgsql(connectionString));

        // 1. Cấu hình ASP.NET Core Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<CulinaryBlogDbContext>();

        // 2. Đăng ký IUnitOfWork
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CulinaryBlogDbContext>());

        // 3. Đăng ký Generic Repository
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // 4. Đăng ký Repositories cụ thể
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // 5. Đăng ký Identity & Helpers
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ISlugHelper, SlugHelper>();
        services.AddScoped<IJwtService, CulinaryBlog.Infrastructure.Auth.JwtService>();
        services.AddScoped<IGoogleTokenValidator, CulinaryBlog.Infrastructure.Auth.GoogleTokenValidator>(); // FR-AUTH-003, D9

        // 6. File Storage thật (MinIO qua S3 API) — CONS-007/D16. Không dùng Mock ở bất kỳ
        // environment nào; integration test tự override bằng FakeFileStorageService riêng.
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var endpoint = config["Minio:Endpoint"] ?? throw new InvalidOperationException("Thiếu Minio:Endpoint.");
            var accessKey = config["Minio:AccessKey"] ?? throw new InvalidOperationException("Thiếu Minio:AccessKey.");
            var secretKey = config["Minio:SecretKey"] ?? throw new InvalidOperationException("Thiếu Minio:SecretKey.");

            return new AmazonS3Client(accessKey, secretKey, new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true, // MinIO dùng path-style (bucket trong path, không phải subdomain).
                UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            });
        });
        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        // 7. Cache thật (Redis, D8 — cache DUY NHẤT, cấm IMemoryCache/Output Cache).
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Redis trong cấu hình.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false; // NFR-REL-002: Redis down vẫn phải khởi động được
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // 8. Email + Background Job thật (Hangfire enqueue — không AddHangfireServer() ở đây:
        // nó resolve JobStorage ngay lúc start, sẽ làm host không lên được nếu Postgres chưa sẵn
        // sàng, vi phạm NFR-REL-002. AddHangfire() một mình chỉ đăng ký IBackgroundJobClient,
        // JobStorage được resolve lazy khi thật sự Enqueue.
        services.AddScoped<IEmailService, MailKitEmailService>();
        services.AddHangfire(config => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
        services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddInfrastructureServices(configuration);
    }
}
