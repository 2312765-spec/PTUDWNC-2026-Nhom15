namespace CulinaryBlog.Domain.Interfaces;

/// <summary>Đảm bảo nhiều thao tác nằm trong một transaction (SRS Phụ lục C — Unit of Work).</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
