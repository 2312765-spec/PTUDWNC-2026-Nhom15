using System.Text;

namespace CulinaryBlog.API.Extensions;

/// <summary>
/// NFR-SEC-007, CONS-004 — fail fast khi thiếu khóa ký JWT. Không có khóa mặc định trong mã nguồn:
/// Development lấy từ appsettings.Development.json / User Secrets, Production từ biến môi trường
/// <c>Jwt__Key</c> (docker-compose.prod.yml, D59).
/// </summary>
public static class JwtKeyGuard
{
    /// <summary>HS256 cần khóa ≥ 256 bit.</summary>
    public const int MinimumKeyBytes = 32;

    public static string GetSigningKey(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"Thiếu cấu hình Jwt:Key hoặc khóa ngắn hơn {MinimumKeyBytes} byte (HS256). " +
                "Production: đặt biến môi trường Jwt__Key.");
        }

        return key;
    }
}
