using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

/// <summary>Hiện thực tối giản của IRepository&lt;T&gt; (Domain) — dùng cho entity chưa cần repository chuyên biệt.</summary>
public sealed class Repository<T>(CulinaryBlogDbContext context) : IRepository<T> where T : BaseEntity
{
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Set<T>().FindAsync([id], ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await context.Set<T>().AddAsync(entity, ct);

    public void Remove(T entity) => context.Set<T>().Remove(entity);
}
