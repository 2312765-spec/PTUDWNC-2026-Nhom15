using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// MinIO (cả Docker Hub lẫn quay.io) đã khóa pull ẩn danh (phát hiện 2026-09-28, PR #17 CI đỏ
/// vì "unauthorized: access to the requested resource is not authorized") — không phải lỗi cấu
/// hình CI, mà MinIO Inc. đã hạn chế phân phối image công khai trên cả hai registry.
///
/// Dùng <c>adobe/s3mock</c> (Adobe, mã nguồn mở, vẫn pull tự do không cần đăng nhập) làm S3 server
/// thật thay thế — test chỉ cần một implementation S3 API thật để xác nhận
/// <c>MinioFileStorageService</c> gọi đúng, không phụ thuộc hành vi riêng của MinIO. S3Mock chỉ hỗ
/// trợ path-style, khớp <c>ForcePathStyle = true</c> đã dùng trong service.
/// </summary>
public static class S3MockContainer
{
    public const int Port = 9090;

    public const string AccessKey = "test";
    public const string SecretKey = "test";

    public static IContainer Build(string bucketName) =>
        new ContainerBuilder()
            .WithImage("adobe/s3mock:latest")
            .WithPortBinding(Port, true)
            .WithEnvironment("COM_ADOBE_TESTING_S3MOCK_STORE_INITIAL_BUCKETS", bucketName)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(Port))
            .Build();

    public static string GetConnectionString(this IContainer container) =>
        $"http://{container.Hostname}:{container.GetMappedPublicPort(Port)}";
}
