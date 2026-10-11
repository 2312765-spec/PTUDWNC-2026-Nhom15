using System.Collections.Concurrent;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.IntegrationTests.Recipes.Support;

/// <summary>
/// Test double cho FR-FILE-001/002. ImagesTests kiểm tra hành vi Command/Handler/Domain/
/// endpoint (validation, ownership, logic IsPrimary...) — KHÔNG kiểm tra việc gọi MinIO thật.
/// Việc đó là test riêng của MinioFileStorageService khi hiện thực Infrastructure (Testcontainers
/// hoặc MinIO thật từ docker-compose), tách biệt khỏi test này để không phụ thuộc hạ tầng ngoài.
///
/// FR-JOB-002: giữ lại nội dung từng object (<see cref="Objects"/>) để job resize tải được ảnh
/// gốc và test đọc được ảnh phái sinh. Đuôi URL lấy từ magic bytes như MinioFileStorageService.
/// </summary>
public sealed class FakeFileStorageService : IFileStorageService
{
    public ConcurrentBag<(string FileName, string ContentType, string Folder)> Uploads { get; } = [];

    public ConcurrentBag<string> DeletedUrls { get; } = [];

    public ConcurrentDictionary<string, byte[]> Objects { get; } = new();

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        content.Position = 0;
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        var extension = ImageSignature.Detect(bytes)?.Extension ?? ".jpg";
        var url = $"https://fake-minio.local/culinary-blog/{folder}/{Guid.NewGuid():N}{extension}";

        Uploads.Add((fileName, contentType, folder));
        Objects[url] = bytes;
        return url;
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        DeletedUrls.Add(fileUrl);
        Objects.TryRemove(fileUrl, out _);
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string fileUrl, CancellationToken ct = default) =>
        Objects.TryGetValue(fileUrl, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
            : throw new FileNotFoundException($"Không có object {fileUrl} trong FakeFileStorageService.");
}
