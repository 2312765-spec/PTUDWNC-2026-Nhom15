using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? CreatedByIp { get; set; }
    public string? JwtId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => RevokedAt == null && !IsExpired;

    public RefreshToken() { }

    public RefreshToken(string userId, string token, DateTime expiresAt, string? createdByIp = null)
    {
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    // Hỗ trợ 3 tham số hoặc 4 tham số
    public static RefreshToken Create(string userId, string token, DateTime expiresAt, object? arg4 = null)
    {
        return new RefreshToken(userId, token, expiresAt, arg4?.ToString());
    }

    public static RefreshToken Create(params object?[] args)
    {
        var userId = args.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
        var token = args.Length > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);
        if (args.Length > 2 && args[2] is DateTime dt) expiresAt = dt;
        var extra = args.Length > 3 ? args[3]?.ToString() : null;
        return new RefreshToken(userId, token, expiresAt, extra);
    }
}