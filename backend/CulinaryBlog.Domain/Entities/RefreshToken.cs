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

    public RefreshToken() { }

    public RefreshToken(string userId, string token, DateTime expiresAt, string? replacedBy = null)
    {
        UserId = userId;
        Token = token;
        TokenHash = token;
        ExpiresAt = expiresAt;
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
        var extra = args.Length > 3 ? args[3]?.ToString() : null;

        return new RefreshToken(userId, token, expiresAt, extra);
    }
}