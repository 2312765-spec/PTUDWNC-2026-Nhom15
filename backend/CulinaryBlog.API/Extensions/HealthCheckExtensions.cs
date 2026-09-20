namespace CulinaryBlog.API.Extensions;

/// <summary>FR-OBS-001 — 3 endpoint health check với mục đích khác nhau.</summary>
public static class HealthCheckExtensions
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var postgres = configuration.GetConnectionString("Postgres");
        var redis = configuration.GetConnectionString("Redis");

        var builder = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(postgres))
        {
            builder.AddNpgSql(postgres, name: "database", tags: [ReadyTag]);
        }

        if (!string.IsNullOrWhiteSpace(redis))
        {
            builder.AddRedis(redis, name: "redis", tags: [ReadyTag]);
        }

        // TODO(S8 — D): thêm AddS3(...) health check cho MinIO.

        return services;
    }
}
