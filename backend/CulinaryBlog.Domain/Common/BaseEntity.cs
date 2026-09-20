namespace CulinaryBlog.Domain.Common;

/// <summary>
/// Lớp cơ sở cho mọi entity (SRS mục 7.1).
/// Soft delete theo D1/D2 — Global Query Filter lọc IsDeleted ở DbContext.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    /// <summary>Set bởi AuditInterceptor khi SaveChanges.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Set bởi AuditInterceptor khi cập nhật.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>D1/D2 — soft delete. Không bao giờ xóa vật lý.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Optimistic concurrency token. Mismatch =&gt; 409 (D4).</summary>
    public byte[] RowVersion { get; set; } = [];

    public void SoftDelete() => IsDeleted = true;

    public void Restore() => IsDeleted = false;
}
