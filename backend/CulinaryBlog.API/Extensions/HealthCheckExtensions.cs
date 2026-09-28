using Amazon.Runtime;
using Amazon.S3;
using HealthChecks.Aws.S3;

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

        // MinIO chỉ nằm trong /health (tổng hợp) — KHÔNG gắn tag "ready", vì SRS chỉ yêu cầu
        // readiness kiểm tra database và Redis.
        var minioEndpoint = configuration["Minio:Endpoint"];
        var minioBucket = configuration["Minio:BucketName"];

        if (!string.IsNullOrWhiteSpace(minioEndpoint) && !string.IsNullOrWhiteSpace(minioBucket))
        {
            builder.AddS3(options =>
            {
                options.BucketName = minioBucket;
                options.Credentials = new BasicAWSCredentials(configuration["Minio:AccessKey"], configuration["Minio:SecretKey"]);
                options.S3Config = new AmazonS3Config
                {
                    ServiceURL = minioEndpoint,
                    ForcePathStyle = true, // MinIO dùng path-style, giống MinioFileStorageService.
                    UseHttp = minioEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
                };
            }, name: "minio");
        }

        return services;
    }
}
