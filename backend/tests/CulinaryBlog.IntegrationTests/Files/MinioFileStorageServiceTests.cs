using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using CulinaryBlog.Infrastructure.Files;
using CulinaryBlog.IntegrationTests.Common;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Files;

/// <summary>
/// FR-FILE-001/002 — gọi thẳng <see cref="MinioFileStorageService"/> vào một S3 server thật
/// (Testcontainers, xem <see cref="S3MockContainer"/> — MinIO đã khóa pull ẩn danh), khác với
/// <c>ImagesTests</c> (dùng <c>FakeFileStorageService</c>, chỉ nhắm Command/Handler/Domain/
/// endpoint). Đóng khoảng trống mà traceability.md từng ghi: PutObjectAsync/DeleteObjectAsync
/// thật chưa được test tự động xác nhận.
/// </summary>
public sealed class MinioFileStorageServiceTests : IAsyncLifetime
{
    private const string BucketName = "culinary-blog-test";

    private readonly IContainer _s3Server = S3MockContainer.Build(BucketName);

    private IAmazonS3 _s3 = null!;
    private MinioFileStorageService _sut = null!;

    public async Task InitializeAsync()
    {
        await _s3Server.StartAsync();

        var endpoint = _s3Server.GetConnectionString();

        _s3 = new AmazonS3Client(S3MockContainer.AccessKey, S3MockContainer.SecretKey, new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            UseHttp = true,
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Minio:BucketName"] = BucketName,
                ["Minio:Endpoint"] = endpoint,
            })
            .Build();

        _sut = new MinioFileStorageService(_s3, configuration);
    }

    public async Task DisposeAsync()
    {
        _s3.Dispose();
        await _s3Server.DisposeAsync().AsTask();
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

    [Fact(DisplayName = "FR-JOB-002: DownloadAsync tải đúng nội dung object đã upload lên MinIO")]
    public async Task DownloadAsync_UploadedObject_ReturnsSameBytes()
    {
        var bytes = JpegBytes();
        using var content = new MemoryStream(bytes);
        var url = await _sut.UploadAsync(content, "a.jpg", "image/jpeg", "recipes/test-recipe", CancellationToken.None);

        await using var downloaded = await _sut.DownloadAsync(url, CancellationToken.None);

        using var buffer = new MemoryStream();
        await downloaded.CopyToAsync(buffer);
        buffer.ToArray().Should().BeEquivalentTo(bytes);
        downloaded.CanSeek.Should().BeTrue("resizer cần stream đọc lại được từ đầu");
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
        var fakeUrl = $"{_s3Server.GetConnectionString()}/{BucketName}/recipes/khong-ton-tai/{Guid.NewGuid()}.jpg";

        var act = () => _sut.DeleteAsync(fakeUrl, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "FR-JOB-003/D52: SaveAsync ghi đúng key cố định, đúng content-type, trả URL công khai")]
    public async Task SaveAsync_FixedKey_StoresObjectWithContentType()
    {
        var bytes = "<urlset/>"u8.ToArray();
        using var content = new MemoryStream(bytes);

        var url = await _sut.SaveAsync("seo/sitemap.xml", content, "application/xml", CancellationToken.None);

        url.Should().EndWith($"/{BucketName}/seo/sitemap.xml");
        using var response = await _s3.GetObjectAsync(BucketName, "seo/sitemap.xml");
        response.Headers.ContentType.Should().Be("application/xml");
        using var responseStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(responseStream);
        responseStream.ToArray().Should().BeEquivalentTo(bytes);
    }

    [Fact(DisplayName = "FR-JOB-003/D52: SaveAsync lần 2 cùng key → ghi đè nội dung, không sinh object mới")]
    public async Task SaveAsync_SameKeyTwice_Overwrites()
    {
        using (var first = new MemoryStream("<old/>"u8.ToArray()))
        {
            await _sut.SaveAsync("seo/sitemap.xml", first, "application/xml", CancellationToken.None);
        }

        var newer = "<new/>"u8.ToArray();
        using (var second = new MemoryStream(newer))
        {
            await _sut.SaveAsync("seo/sitemap.xml", second, "application/xml", CancellationToken.None);
        }

        var listing = await _s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BucketName, Prefix = "seo/" });
        listing.S3Objects.Should().ContainSingle().Which.Key.Should().Be("seo/sitemap.xml");

        using var response = await _s3.GetObjectAsync(BucketName, "seo/sitemap.xml");
        using var responseStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(responseStream);
        responseStream.ToArray().Should().BeEquivalentTo(newer);
    }

    [Fact(DisplayName = "CONS-009/D58: có Minio:PublicBaseUrl → URL ảnh trả cho trình duyệt dùng PublicBaseUrl")]
    public async Task UploadAsync_WithPublicBaseUrl_ReturnsPublicUrl()
    {
        var sut = CreateSut(publicBaseUrl: "https://culinary.example/");
        using var content = new MemoryStream(JpegBytes());

        var url = await sut.UploadAsync(content, "a.jpg", "image/jpeg", "recipes/test-recipe", CancellationToken.None);

        url.Should().StartWith($"https://culinary.example/{BucketName}/recipes/test-recipe/");
        using var response = await _s3.GetObjectAsync(BucketName, ExtractKey(url));
        response.HttpStatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CONS-009/D58: Minio:PublicBaseUrl rỗng → URL vẫn ghép từ Minio:Endpoint (dev không đổi)")]
    public async Task UploadAsync_EmptyPublicBaseUrl_FallsBackToEndpoint()
    {
        var sut = CreateSut(publicBaseUrl: "");
        using var content = new MemoryStream(JpegBytes());

        var url = await sut.UploadAsync(content, "a.jpg", "image/jpeg", "recipes/test-recipe", CancellationToken.None);

        url.Should().StartWith($"{_s3Server.GetConnectionString().TrimEnd('/')}/{BucketName}/");
    }

    [Fact(DisplayName = "CONS-009/D58: URL công khai vẫn xóa được object thật trên MinIO")]
    public async Task DeleteAsync_PublicUrl_RemovesFromMinio()
    {
        var sut = CreateSut(publicBaseUrl: "https://culinary.example");
        using var content = new MemoryStream(JpegBytes());
        var url = await sut.UploadAsync(content, "a.jpg", "image/jpeg", "recipes/test-recipe", CancellationToken.None);

        await sut.DeleteAsync(url, CancellationToken.None);

        var act = () => _s3.GetObjectAsync(BucketName, ExtractKey(url));
        var exception = await act.Should().ThrowAsync<AmazonS3Exception>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private MinioFileStorageService CreateSut(string publicBaseUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Minio:BucketName"] = BucketName,
                ["Minio:Endpoint"] = _s3Server.GetConnectionString(),
                ["Minio:PublicBaseUrl"] = publicBaseUrl,
            })
            .Build();

        return new MinioFileStorageService(_s3, configuration);
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
