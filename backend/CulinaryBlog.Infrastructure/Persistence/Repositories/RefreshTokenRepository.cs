using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(CulinaryBlogDbContext dbContext) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await dbContext.RefreshTokens.AddAsync(token, ct);


    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) => Task.FromResult<RefreshToken?>(null);
public Task AddAsync(string token, string userId, DateTime expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
public Task RevokeAsync(string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
public Task<bool> IsValidAsync(string token, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
