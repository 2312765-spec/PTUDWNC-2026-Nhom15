using Microsoft.AspNetCore.Hosting;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// NFR-SEC-003/D15 — bật rate limiting (mặc định TẮT ở môi trường Testing để test khác không
/// chạm giới hạn lẫn nhau) với ngưỡng nhỏ để test vượt giới hạn nhanh. Không cần DB/Redis:
/// middleware chạy trước routing nên request bị từ chối (hoặc 401/404 ở các route thử) không đụng hạ tầng.
/// </summary>
public sealed class RateLimitApiFactory : CulinaryBlogApiFactory
{
    public const int AuthLimit = 3;
    public const int GeneralLimit = 5;
    public const int UploadLimit = 2;
    public const int WindowSeconds = 60;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:WindowSeconds", WindowSeconds.ToString());
        builder.UseSetting("RateLimiting:Auth:PermitLimit", AuthLimit.ToString());
        builder.UseSetting("RateLimiting:General:PermitLimit", GeneralLimit.ToString());
        builder.UseSetting("RateLimiting:Upload:PermitLimit", UploadLimit.ToString());
    }
}
