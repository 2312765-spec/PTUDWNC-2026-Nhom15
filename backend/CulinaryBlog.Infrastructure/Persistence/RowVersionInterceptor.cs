using System.Security.Cryptography;
using CulinaryBlog.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// D34 — sinh RowVersion mới cho mỗi lần ghi. SRS 7.1 ghi "[Timestamp]", nhưng đó là cơ chế của
/// SQL Server (DB tự tăng rowversion); PostgreSQL không tự sinh gì cho cột bytea, nên thiếu
/// interceptor này thì "WHERE RowVersion = ''" luôn đúng và concurrency token vô tác dụng.
///
/// Chỉ đổi giá trị HIỆN TẠI: EF vẫn dùng giá trị GỐC (đọc từ DB) trong mệnh đề WHERE, nên nếu
/// người khác đã lưu trước thì UPDATE ảnh hưởng 0 dòng → DbUpdateConcurrencyException → 409 (D4).
/// </summary>
public sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyRowVersion(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyRowVersion(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyRowVersion(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified || HasChangedOwnedEntity(entry))
            {
                entry.Entity.RowVersion = RandomNumberGenerator.GetBytes(16);
            }
        }
    }

    // Owned entity (vd. Recipe.Nutrition) nằm chung dòng với entity chủ nhưng có entry riêng: sửa
    // mỗi phần owned thì entry chủ vẫn Unchanged — vẫn phải đổi RowVersion của chủ.
    private static bool HasChangedOwnedEntity(EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry is { } target
            && target.Metadata.IsOwned()
            && target.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
}
