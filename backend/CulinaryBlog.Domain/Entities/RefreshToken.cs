using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    public RefreshToken() { }

    public RefreshToken(string userId, string token, DateTime expiresAt, string? createdByIp = null, string? replacedBy = null)
    {
        UserId = userId;
        Token = token;
        TokenHash = token;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        ReplacedByTokenHash = replacedBy;
    }

    public void Revoke(string? replacedByTokenHash = null)
    {
        IsRevoked = true;
        ReplacedByTokenHash = replacedByTokenHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public static RefreshToken Create(params object?[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return new RefreshToken();
        }

        var userId = args.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
        var token = args.Length > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;
        var expiresAt = DateTime.UtcNow.AddDays(7);
        if (args.Length > 2 && args[2] is DateTime dt)
        {
            expiresAt = dt;
        }

        var ipOrExtra = args.Length > 3 ? args[3]?.ToString() : null;
        var replaced = args.Length > 4 ? args[4]?.ToString() : null;

        return new RefreshToken(userId, token, expiresAt, ipOrExtra, replaced);
    }
}