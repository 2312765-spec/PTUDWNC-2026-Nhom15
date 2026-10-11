namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Thực thể RefreshToken — SRS 7.8, Quyết định D20.
/// Bảng Database gồm: Id, UserId, TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedAt, CreatedByIp.
/// Chỉ lưu HASH của token (D25) — raw token không bao giờ chạm DB.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserId { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken()
    {
    }

    private RefreshToken(string userId, string tokenHash, DateTime expiresAt, string? createdByIp)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>D20/D25 — tạo refresh token mới. tokenHash PHẢI là hash (SHA-256) của raw token, không phải raw token.</summary>
    public static RefreshToken Create(string userId, string tokenHash, DateTime expiresAt, string? createdByIp = null)
        => new(userId, tokenHash, expiresAt, createdByIp);

    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Chỉ dùng khi Infrastructure phát hiện trùng TokenHash (xác suất cực thấp) — sinh lại hash mới.</summary>
    public void ReassignTokenHash(string tokenHash) => TokenHash = tokenHash;
}
