using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

/// <summary>
/// Riêng cho <see cref="RefreshToken"/> vì entity này không kế thừa BaseEntity
/// (D20 — schema 7.8 không có IsDeleted/RowVersion) nên không khớp <see cref="IRepository{T}"/>.
/// </summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
}
