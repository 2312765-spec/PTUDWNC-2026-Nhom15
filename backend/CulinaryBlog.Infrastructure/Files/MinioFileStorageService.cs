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

        var endpoint = (configuration["Minio:Endpoint"] ?? throw new InvalidOperationException("Thiếu Minio:Endpoint.")).TrimEnd('/');
        return $"{endpoint}/{BucketName}/{key}";
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

    private string ExtractKey(string fileUrl)
    {
        var marker = $"/{BucketName}/";
        var index = fileUrl.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? fileUrl : fileUrl[(index + marker.Length)..];
    }
}
