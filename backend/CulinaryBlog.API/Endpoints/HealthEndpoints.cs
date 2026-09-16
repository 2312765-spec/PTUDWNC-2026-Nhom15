using CulinaryBlog.API.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace CulinaryBlog.API.Endpoints;

/// <summary>FR-OBS-001 — chủ sở hữu: D.</summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Tổng hợp tất cả component (database, redis, minio)
        app.MapHealthChecks("/health");

        // Liveness — chỉ kiểm tra process còn sống. Luôn healthy trừ khi process chết.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        // Readiness — kiểm tra DB và Redis. Fail => Nginx/K8s ngừng route traffic.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(HealthCheckExtensions.ReadyTag),
        });
    }
}
