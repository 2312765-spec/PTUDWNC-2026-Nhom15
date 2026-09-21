using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(CulinaryBlogDbContext dbContext) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await dbContext.RefreshTokens.AddAsync(token, ct);
}
