using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CulinaryBlog.Infrastructure.Files;

/// <summary>
/// FR-FILE-001/002, D16 (bucket public-read), D28 (extension từ magic bytes, không tin
/// fileName/contentType client gửi). UploadRecipeImageCommandValidator đã kiểm tra magic bytes
/// trước khi tới đây — Detect lại ở đây không phải validate thêm, mà vì đây là nơi DUY NHẤT
/// quyết định object key/extension thật sự dùng để lưu (interface không nhận extension riêng).
/// </summary>
public sealed class MinioFileStorageService(IAmazonS3 s3Client, IConfiguration configuration) : IFileStorageService
{
    private string BucketName => configuration["Minio:BucketName"] ?? "culinary-blog";

    public async Task<string> UploadAsync(
        Stream content, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        content.Position = 0;
        var header = new byte[32];
        var read = await content.ReadAsync(header.AsMemory(), ct);
        content.Position = 0;

        var signature = ImageSignature.Detect(header.AsSpan(0, read))
            ?? throw new InvalidOperationException(
                "Nội dung không phải ảnh hợp lệ dù đã qua UploadRecipeImageCommandValidator — kiểm tra lại pipeline.");

        var key = ObjectKey.Create(folder, signature.Extension);

        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = BucketName,
            Key = key,
            InputStream = content,
            ContentType = signature.ContentType,
            AutoCloseStream = false,
        }, ct);

        return PublicUrl(key);
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        try
        {
            await s3Client.DeleteObjectAsync(BucketName, ExtractKey(fileUrl), ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // FR-FILE-002: idempotent — object không tồn tại thì không throw.
        }
    }

    /// <summary>
    /// FR-JOB-002 — tải object về bộ nhớ (file ≤ 5 MB theo CONS-007) để trả stream seek được:
    /// resizer cần đọc lại từ đầu, ResponseStream của S3 không làm được việc đó.
    /// </summary>
    public async Task<Stream> DownloadAsync(string fileUrl, CancellationToken ct = default)
    {
        using var response = await s3Client.GetObjectAsync(BucketName, ExtractKey(fileUrl), ct);
        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    /// FR-JOB-003/D52 — file hệ thống sinh (sitemap.xml): key cố định, ghi đè, không qua ImageSignature.
    /// </summary>
    public async Task<string> SaveAsync(string objectKey, Stream content, string contentType, CancellationToken ct = default)
    {
        content.Position = 0;
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = BucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, ct);

        return PublicUrl(objectKey);
    }

    /// <summary>
    /// D58 — URL trình duyệt dùng. Prod: <c>Minio:PublicBaseUrl</c> = domain của site (Nginx proxy
    /// <c>/{bucket}/</c> tới MinIO nội bộ). Rỗng → <c>Minio:Endpoint</c> (dev: http://localhost:9000).
    /// </summary>
    private string PublicUrl(string key)
    {
        var baseUrl = configuration["Minio:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = configuration["Minio:Endpoint"] ?? throw new InvalidOperationException("Thiếu Minio:Endpoint.");
        }

        return $"{baseUrl.TrimEnd('/')}/{BucketName}/{key}";
    }

    private string ExtractKey(string fileUrl)
    {
        var marker = $"/{BucketName}/";
        var index = fileUrl.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? fileUrl : fileUrl[(index + marker.Length)..];
    }
}
