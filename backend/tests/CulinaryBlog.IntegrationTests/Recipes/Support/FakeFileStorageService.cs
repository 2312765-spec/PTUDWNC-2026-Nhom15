using System.Collections.Concurrent;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.IntegrationTests.Recipes.Support;

/// <summary>
/// Test double cho FR-FILE-001/002. ImagesTests kiểm tra hành vi Command/Handler/Domain/
/// endpoint (validation, ownership, logic IsPrimary...) — KHÔNG kiểm tra việc gọi MinIO thật.
/// Việc đó là test riêng của MinioFileStorageService khi hiện thực Infrastructure (Testcontainers
/// hoặc MinIO thật từ docker-compose), tách biệt khỏi test này để không phụ thuộc hạ tầng ngoài.
/// </summary>
public sealed class FakeFileStorageService : IFileStorageService
{
    public ConcurrentBag<(string FileName, string ContentType, string Folder)> Uploads { get; } = [];

    public ConcurrentBag<string> DeletedUrls { get; } = [];

    public Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        Uploads.Add((fileName, contentType, folder));
        return Task.FromResult($"https://fake-minio.local/culinary-blog/{folder}/{Guid.NewGuid():N}.jpg");
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        DeletedUrls.Add(fileUrl);
        return Task.CompletedTask;
    }
}
