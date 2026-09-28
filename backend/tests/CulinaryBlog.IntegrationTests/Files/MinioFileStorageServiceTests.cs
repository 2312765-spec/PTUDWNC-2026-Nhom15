using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using CulinaryBlog.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Testcontainers.Minio;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Files;

/// <summary>
/// FR-FILE-001/002 — gọi thẳng <see cref="MinioFileStorageService"/> vào một MinIO thật
/// (Testcontainers), khác với <c>ImagesTests</c> (dùng <c>FakeFileStorageService</c>, chỉ nhắm
/// Command/Handler/Domain/endpoint). Đóng khoảng trống mà traceability.md từng ghi: PutObjectAsync/
/// DeleteObjectAsync thật chưa được test tự động xác nhận.
/// </summary>
public sealed class MinioFileStorageServiceTests : IAsyncLifetime
{
    private const string BucketName = "culinary-blog-test";

    // Cùng image với docker-compose.yml (quay.io, không dùng Docker Hub "minio/minio" — bị
    // chặn pull ẩn danh, xem ghi chú ở docker-compose.yml).
    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("quay.io/minio/minio:latest")
        .Build();

    private IAmazonS3 _s3 = null!;
    private MinioFileStorageService _sut = null!;

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();

        _s3 = new AmazonS3Client(_minio.GetAccessKey(), _minio.GetSecretKey(), new AmazonS3Config
        {
            ServiceURL = _minio.GetConnectionString(),
            ForcePathStyle = true,
            UseHttp = true,
        });

        await _s3.PutBucketAsync(BucketName);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Minio:BucketName"] = BucketName,
                ["Minio:Endpoint"] = _minio.GetConnectionString(),
            })
            .Build();

        _sut = new MinioFileStorageService(_s3, configuration);
    }

    public async Task DisposeAsync()
    {
        _s3.Dispose();
        await _minio.DisposeAsync().AsTask();
    }

    [Fact(DisplayName = "FR-FILE-001/D16: upload ảnh JPEG thật lên MinIO qua S3 API")]
    public async Task UploadAsync_RealJpeg_StoresObjectOnMinio()
    {
        var bytes = JpegBytes();
        using var content = new MemoryStream(bytes);

        var url = await _sut.UploadAsync(content, "anything.jpg", "image/jpeg", "recipes/test-recipe", CancellationToken.None);

        url.Should().Contain($"/{BucketName}/recipes/test-recipe/");
        url.Should().EndWith(".jpg");

        var key = ExtractKey(url);
        using var response = await _s3.GetObjectAsync(BucketName, key);
        response.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ContentType.Should().Be("image/jpeg");

        using var responseStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(responseStream);
        responseStream.ToArray().Should().BeEquivalentTo(bytes);
    }

    [Fact(DisplayName = "FR-FILE-002: xóa object đã tồn tại thì object biến mất thật trên MinIO")]
    public async Task DeleteAsync_ExistingObject_RemovesFromMinio()
    {
        using var content = new MemoryStream(JpegBytes());
        var url = await _sut.UploadAsync(content, "anything.jpg", "image/jpeg", "recipes/test-recipe", CancellationToken.None);
        var key = ExtractKey(url);

        await _sut.DeleteAsync(url, CancellationToken.None);

        var act = () => _s3.GetObjectAsync(BucketName, key);
        var exception = await act.Should().ThrowAsync<AmazonS3Exception>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "FR-FILE-002/D1: xóa object không tồn tại trên MinIO thật vẫn idempotent, không throw")]
    public async Task DeleteAsync_NonExistentObject_DoesNotThrow()
    {
        var fakeUrl = $"{_minio.GetConnectionString()}/{BucketName}/recipes/khong-ton-tai/{Guid.NewGuid()}.jpg";

        var act = () => _sut.DeleteAsync(fakeUrl, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static byte[] JpegBytes()
    {
        // FF D8 FF = magic bytes JPEG (đủ để ImageSignature.Detect nhận diện trong MinioFileStorageService).
        var bytes = new byte[64];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE0;
        return bytes;
    }

    private static string ExtractKey(string url)
    {
        var marker = $"/{BucketName}/";
        var index = url.IndexOf(marker, StringComparison.Ordinal);
        return url[(index + marker.Length)..];
    }
}
