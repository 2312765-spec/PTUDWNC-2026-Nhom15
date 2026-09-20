namespace CulinaryBlog.Infrastructure.Identity;

/// <summary>
/// SRS 7.8 / D20 — KHÔNG kế thừa BaseEntity (không có IsDeleted/RowVersion trong schema 7.8).
/// KHÔNG lưu raw token, chỉ lưu SHA-256 hash. KHÔNG có cột IsRevoked — tính từ RevokedAt.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string UserId { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; private set; }

    /// <summary>D20 — computed, không phải cột DB.</summary>
    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;

    private RefreshToken()
    {
    }

    public static RefreshToken Create(string userId, string tokenHash, DateTime expiresAt, string? createdByIp = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId không được để trống.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("TokenHash không được để trống.", nameof(tokenHash));
        }

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp,
        };
    }

    /// <summary>Đánh dấu revoke — dùng khi rotation hoặc reuse detection (D20).</summary>
    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
