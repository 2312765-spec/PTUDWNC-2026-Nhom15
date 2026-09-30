using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Tra theo SHA-256 hash (D20) — caller phải hash raw token trước khi gọi.</summary>
    Task<RefreshToken?> GetByTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task AddAsync(string userId, string token, DateTime expiresAt = default, CancellationToken cancellationToken = default);
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> IsValidAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-AUTH-004, D35-6 — revoke token (rotation) chỉ khi nó CHƯA bị revoke, ghi
    /// <c>ReplacedByTokenHash</c>. Thực thi ngay trên DB (không chờ SaveChanges).
    /// Trả <c>false</c> nếu request khác đã revoke trước (thua race).
    /// </summary>
    Task<bool> TryRevokeAsync(string tokenHash, string replacedByTokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-AUTH-004 A3, NFR-SEC-002, D35-5 — reuse detection: revoke mọi RT còn hiệu lực của user.
    /// Thực thi ngay trên DB (không chờ SaveChanges). Trả số token đã revoke.
    /// </summary>
    Task<int> RevokeAllActiveForUserAsync(string userId, CancellationToken cancellationToken = default);
}
