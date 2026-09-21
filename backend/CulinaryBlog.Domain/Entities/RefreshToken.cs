namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// SRS 7.8. Theo D20: schema đúng có <see cref="TokenHash"/>/<see cref="RevokedAt"/>/
/// <see cref="ReplacedByTokenHash"/> — KHÔNG có cột IsRevoked (computed từ RevokedAt),
/// KHÔNG có ReplacedByToken (chỉ có hash). KHÔNG kế thừa BaseEntity (không IsDeleted/RowVersion —
/// D20 không liệt hai cột đó trong schema 7.8).
///
/// UserId kiểu string để khớp AspNetUsers.Id (D23: ApplicationUser ở Infrastructure,
/// Domain không có navigation property tới nó).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string UserId { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    /// <summary>D20 — computed, không phải cột DB.</summary>
    public bool IsRevoked => RevokedAt != null;

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;

    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken()
    {
    }

    public static RefreshToken Create(string userId, string tokenHash, DateTime expiresAt, string? createdByIp)
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = createdByIp,
        };
    }

    /// <summary>D20/NFR-SEC-002 — token rotation: token cũ bị revoke, trỏ sang token mới.</summary>
    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
