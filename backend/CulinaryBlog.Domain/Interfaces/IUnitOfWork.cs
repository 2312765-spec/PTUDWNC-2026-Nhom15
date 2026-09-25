namespace CulinaryBlog.Domain.Interfaces;

/// <summary>Đảm bảo nhiều thao tác nằm trong một transaction (SRS Phụ lục C — Unit of Work).</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Chạy <paramref name="operation"/> trong MỘT transaction (nhiều lần <see cref="SaveChangesAsync"/> bên
    /// trong hoặc cùng commit hoặc cùng rollback). Khi lỗi tạm thời (transient) thì cả khối được chạy lại
    /// từ đầu trên tracker sạch, nên <paramref name="operation"/> phải TỰ nạp entity bên trong và không
    /// giữ entity đã nạp từ bên ngoài.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
