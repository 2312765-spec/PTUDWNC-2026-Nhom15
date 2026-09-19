namespace CulinaryBlog.Domain.Interfaces;

/// <summary>
/// Unit of Work pattern theo đặc tả SRS v1.0.0
/// Đảm bảo tính toàn vẹn giao dịch và cung cấp các Repositories
/// </summary>
public interface IUnitOfWork : IDisposable
{
    ICategoryRepository Categories { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
