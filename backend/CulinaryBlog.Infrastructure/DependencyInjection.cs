using Amazon.S3;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Auth;
using CulinaryBlog.Infrastructure.Caching;
using CulinaryBlog.Infrastructure.Email;
using CulinaryBlog.Infrastructure.Files;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Infrastructure.Repositories;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
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

        // ---- ASP.NET Core Identity (S2 — A, D23/ADR-0003) ----
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // NFR-SEC-001: khớp RegisterCommandValidator — Identity là phòng thủ chiều sâu.
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                // D17: khóa 15 phút sau 5 lần sai.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CulinaryBlogDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailService, MailKitEmailService>();

        // ---- Hangfire (FR-JOB-001 — chỉ phần enqueue dùng ngay từ FR-AUTH-001) ----
        // CHỦ Ý không gọi AddHangfireServer() ở đây: nó resolve JobStorage NGAY lúc host
        // start (Host.StartAsync → ThrowIfNotConfigured), tức là app không khởi động được
        // nếu Postgres chưa sẵn sàng — vi phạm tinh thần NFR-REL-002 và làm hỏng test không
        // cần DB thật (SmokeTests). AddHangfire() một mình chỉ đăng ký IBackgroundJobClient,
        // JobStorage được resolve LAZY khi thật sự Enqueue. Server xử lý job (worker) là việc
        // của FR-JOB-001/S9 — thêm AddHangfireServer() ở đó khi đã có job thật cần chạy.
        services.AddHangfire(config => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
        services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();

        // ---- Category (FR-CAT-001 — B) ----
        // Bug phát hiện lúc rebase PR (2026-09-22): thiếu đăng ký này thì GetCategoriesQueryHandler
        // không resolve được ICategoryRepository → GET /api/v1/categories trả 500.
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        // ---- Recipe images (FR-RCP-008/FR-FILE-001/002 — D, S8) ----
        // IRecipeRepository chỉ có 1 method (GetByIdWithImagesAsync) — đủ cho S8. FR-RCP-001..007
        // (C, S7) sẽ thêm method khác vào cùng interface khi tới lượt, không tạo interface riêng.
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

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

        // TODO(S3 — B): ISlugHelper

        return services;
    }
}
