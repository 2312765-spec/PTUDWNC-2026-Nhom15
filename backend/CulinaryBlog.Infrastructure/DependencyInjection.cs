using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Domain.Interfaces; // Hoặc CulinaryBlog.Application.Interfaces tùy vị trí chứa ICategoryRepository

namespace CulinaryBlog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            // Dùng In-Memory Database cho phát triển nhanh và test
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("CulinaryBlogDb"));
        }
        else
        {
            // Kết nối PostgreSQL 16 theo tài liệu kiến trúc SRS
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
