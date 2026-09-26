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
            token.TokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        }

        await dbContext.RefreshTokens.AddAsync(token, ct);
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == token, cancellationToken);
    }

    public async Task AddAsync(string token, string userId, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var refreshToken = new RefreshToken(userId, token, expiresAt);
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