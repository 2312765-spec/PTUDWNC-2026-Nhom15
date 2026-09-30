using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(CulinaryBlogDbContext dbContext) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        // Kiểm tra xem TokenHash này đã tồn tại trong DB chưa để không bao giờ bị dính lỗi 23505
        var exists = await dbContext.RefreshTokens
            .AnyAsync(r => r.TokenHash == token.TokenHash, ct);

        if (exists)
        {
            // Nếu đã tồn tại thì sinh một hash mới duy nhất trước khi chèn vào
            token.ReassignTokenHash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        }

        await dbContext.RefreshTokens.AddAsync(token, ct);
    }

    public async Task<RefreshToken?> GetByTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<bool> TryRevokeAsync(string tokenHash, string replacedByTokenHash, CancellationToken cancellationToken = default)
    {
        // D35-6 — điều kiện RevokedAt == null nằm trong chính câu UPDATE, nên chỉ một trong hai
        // request đồng thời cập nhật được dòng này.
        var now = DateTime.UtcNow;
        var affected = await dbContext.RefreshTokens
            .Where(r => r.TokenHash == tokenHash && r.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.RevokedAt, now)
                    .SetProperty(r => r.ReplacedByTokenHash, replacedByTokenHash),
                cancellationToken);

        return affected == 1;
    }

    public async Task<int> RevokeAllActiveForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await dbContext.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, now), cancellationToken);
    }

    public async Task AddAsync(string userId, string token, DateTime expiresAt = default, CancellationToken cancellationToken = default)
    {
        var refreshToken = RefreshToken.Create(userId, token, expiresAt);
        await AddAsync(refreshToken, cancellationToken);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == token, cancellationToken);

        if (existing != null)
        {
            existing.Revoke();
        }
    }

    public async Task<bool> IsValidAsync(string token, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == token, cancellationToken);

        return existing != null && existing.IsActive;
    }
}