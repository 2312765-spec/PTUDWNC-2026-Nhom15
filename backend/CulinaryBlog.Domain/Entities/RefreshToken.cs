using System.ComponentModel.DataAnnotations.Schema;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Thực thể RefreshToken — SRS 7.8, Quyết định D20.
/// Bảng Database gồm: Id, UserId, TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedAt, CreatedByIp.
/// </summary>
public class RefreshToken 
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }

    [NotMapped]
    public DateTime? UpdatedAt { get; set; }

    [NotMapped]
    public string Token { get; set; } = string.Empty;

    [NotMapped]
    public bool IsRevoked => RevokedAt.HasValue;

    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [NotMapped]
    public bool IsActive => !IsRevoked && !IsExpired;

    public RefreshToken() 
    {
        TokenHash = Guid.NewGuid().ToString("N");
    }

    public RefreshToken(string userId, string tokenHash, DateTime expiresAt, string? createdByIp = null, string? replacedBy = null)
    {
        UserId = userId;
        Token = tokenHash;
        // Nếu tokenHash rỗng thì tự sinh GUID ngẫu nhiên để tránh lỗi trùng lặp Unique Index trong DB
        TokenHash = string.IsNullOrWhiteSpace(tokenHash) ? Guid.NewGuid().ToString("N") : tokenHash;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        ReplacedByTokenHash = replacedBy;
        CreatedAt = DateTime.UtcNow;
    }
   
    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAt = DateTime.UtcNow;
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