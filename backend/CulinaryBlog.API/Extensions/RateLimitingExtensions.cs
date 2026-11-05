namespace CulinaryBlog.API.Extensions;

/// <summary>Cấu hình mục <c>RateLimiting</c> trong appsettings. Giá trị mặc định = D15.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Để trống = bật ở mọi môi trường trừ Testing (test khác không được chạm giới hạn của nhau).</summary>
    public bool? Enabled { get; set; }

    public int WindowSeconds { get; set; } = 60;

    public GroupOptions Auth { get; set; } = new() { PermitLimit = 10 };
    public GroupOptions Upload { get; set; } = new() { PermitLimit = 5 };
    public GroupOptions General { get; set; } = new() { PermitLimit = 100 };

    public bool IsEnabled(IHostEnvironment environment) => Enabled ?? !environment.IsEnvironment("Testing");

    public sealed class GroupOptions
    {
        public int PermitLimit { get; set; }
    }
}

public static class RateLimitingExtensions
{
    /// <summary>
    /// Đăng ký bộ đếm. IP máy khách lấy từ <c>RemoteIpAddress</c> sau <c>UseForwardedHeaders</c> (D60, Program.cs):
    /// không có bước đó thì sau Nginx mọi người dùng chung một bộ đếm. Request server → API của Next.js
    /// (đăng nhập, refresh, logout) không qua Nginx — frontend tự gửi <c>X-Forwarded-For</c> (D62).
    /// </summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, RateLimitingOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<RequestRateLimiter>();
        return services;
    }
}
