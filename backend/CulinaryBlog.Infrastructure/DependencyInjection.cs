using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Infrastructure.Repositories;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
       var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<CulinaryBlogDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString);
            }
        });
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
        services.AddScoped<IJwtService, MockJwtService>();

        // 6. Đăng ký Mock Service cho File Storage và Background Job
        services.AddScoped<IFileStorageService, MockFileStorageService>();
        services.AddScoped<IBackgroundJobService, MockBackgroundJobService>();

        // 7. Đăng ký CacheService (giải quyết triệt để lỗi MediatR CacheInvalidationBehavior)
        services.AddScoped<ICacheService, MockCacheService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        return services.AddInfrastructureServices(configuration);
    }
}

// Mock ICacheService chuẩn xác 100% theo hợp đồng giao diện Quyết định D8
public class MockCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(
        string key, 
        T value, 
        TimeSpan expiration, 
        IEnumerable<string> tags, 
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

// Giả lập Mock JWT Service
// Giả lập Mock JWT Service khớp 100% tất cả các phương thức của IJwtService
public class MockJwtService : IJwtService
{
    public (string Token, DateTime ExpiresAt) GenerateAccessToken(string userId, string email, IEnumerable<string> roles)
        => ("mock-jwt-access-token", DateTime.UtcNow.AddHours(2));

    public (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken()
        => ("mock-raw-token", "mock-token-hash", DateTime.UtcNow.AddDays(7));

    public string HashToken(string token)
        => "mock-hashed-" + token;

    public System.Security.Claims.ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        => new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
}

// Giả lập Mock File Storage Service
public class MockFileStorageService : IFileStorageService
{
    public Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
        => Task.FromResult($"https://localhost:7001/uploads/{folder}/{fileName}");

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// Giả lập Mock Background Job Service
public class MockBackgroundJobService : IBackgroundJobService
{
    public void EnqueueWelcomeEmail(string email, string displayName) { }
    public void EnqueueDeleteImageFile(string fileUrl) { }
}